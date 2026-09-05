using System.Collections.Concurrent;

namespace Monjo
{
    /// <summary>
    /// Runs one-time per-entity initialization (schema/index DDL) exactly once per key per process.
    /// Hot-path cost after the first run: one lock-free dictionary lookup + an IsCompleted
    /// check — no allocations (callers cache the key string and work delegate per entity type,
    /// see <c>EnsureEntityReadyAsync</c> in each provider connection). On failure the gate entry
    /// is removed so the next call retries.
    /// </summary>
    public static class EntityReadinessGate
    {
        private sealed class Entry
        {
            public readonly TaskCompletionSource<bool> Tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static readonly ConcurrentDictionary<string, Entry> _entries = new();

        public static Task EnsureAsync(string key, Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                if (_entries.TryGetValue(key, out var existing))
                {
                    var task = existing.Tcs.Task;
                    if (task.IsCompleted)
                        return Task.CompletedTask;

                    // Initialization is already in flight (or failed and not yet cleaned up):
                    // join the owner's task. No allocation on either path.
                    return task;
                }

                // First caller for this key. The Entry is allocated only here — once per key per
                // process — never on the completed fast path above.
                var entry = new Entry();
                if (_entries.TryAdd(key, entry))
                    return RunWorkAsync(key, entry, work, cancellationToken);

                // Lost the TryAdd race to another owner: loop and join its entry (or become the
                // owner ourselves if it failed and was already removed).
            }
        }

        private static async Task RunWorkAsync(string key, Entry entry, Func<CancellationToken, Task> work, CancellationToken cancellationToken)
        {
            try
            {
                await work(cancellationToken).ConfigureAwait(false);
                entry.Tcs.TrySetResult(true);
            }
            catch (Exception e)
            {
                // Remove so the next caller retries; concurrent joiners observe the failure
                // through the TCS task.
                _entries.TryRemove(key, out _);
                entry.Tcs.TrySetException(e);
                throw;
            }
        }
    }
}
