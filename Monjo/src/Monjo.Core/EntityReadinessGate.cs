using System.Collections.Concurrent;

namespace Monjo
{
    /// <summary>
    /// Runs one-time per-entity initialization (schema/index DDL) exactly once per key per process.
    /// Hot-path cost after the first run: one lock-free dictionary lookup + awaiting an already
    /// completed task (no allocations). On failure the gate is removed so the next call retries.
    /// </summary>
    public static class EntityReadinessGate
    {
        private sealed class Entry
        {
            public readonly TaskCompletionSource<bool> Tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static readonly ConcurrentDictionary<string, Entry> _entries = new();

        public static async Task EnsureAsync(string key, Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
        {
            Entry entry;
            var isOwner = false;
            var factory = (string _) =>
            {
                isOwner = true;
                return new Entry();
            };
            entry = _entries.GetOrAdd(key, factory);

            if (!isOwner)
            {
                await entry.Tcs.Task.ConfigureAwait(false);
                return;
            }

            try
            {
                await work(cancellationToken).ConfigureAwait(false);
                entry.Tcs.TrySetResult(true);
            }
            catch (Exception e)
            {
                _entries.TryRemove(key, out _);
                entry.Tcs.TrySetException(e);
                throw;
            }
        }
    }
}
