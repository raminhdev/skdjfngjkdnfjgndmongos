namespace Monjo
{
    /// <summary>
    /// A provider-native transaction exposed through a common, capability-based API:
    /// <see cref="CommitAsync"/> / <see cref="RollbackAsync"/> / <see cref="DisposeAsync"/>
    /// (disposal rolls back when not yet committed).
    /// </summary>
    /// <remarks>
    /// While open, the transaction is ambient for the current async scope: repository operations
    /// started inside the scope enlist automatically (Mongo passes its <c>IClientSessionHandle</c>,
    /// SQL attaches the native <c>DbTransaction</c> to commands). There are no fake universal
    /// semantics — each provider commits/rolls back with its own native call.
    /// </remarks>
    public sealed class MonjoTransaction : IAsyncDisposable
    {
        private const int StateOpen = 0;
        private const int StateCommitted = 1;
        private const int StateRolledBack = 2;

        private readonly Func<CancellationToken, Task> _commit;
        private readonly Func<CancellationToken, Task> _rollback;
        private readonly Func<ValueTask> _disposeNative;
        private int _state;

        /// <summary>Provider-native bridge object (e.g. the Mongo session bridge or SQL transaction bridge).</summary>
        internal object Native { get; }

        internal MonjoTransaction(
            object native,
            Func<CancellationToken, Task> commit,
            Func<CancellationToken, Task> rollback,
            Func<ValueTask> disposeNative)
        {
            Native = native ?? throw new ArgumentNullException(nameof(native));
            _commit = commit ?? throw new ArgumentNullException(nameof(commit));
            _rollback = rollback ?? throw new ArgumentNullException(nameof(rollback));
            _disposeNative = disposeNative ?? throw new ArgumentNullException(nameof(disposeNative));

            MonjoTransactionContext.Set(this);
        }

        /// <summary>True while the transaction is open (not committed and not rolled back).</summary>
        public bool IsOpen => _state == StateOpen;

        /// <summary>Commits with the provider-native commit. Throws <see cref="InvalidOperationException"/> when already completed.</summary>
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _state, StateCommitted) != StateOpen)
                throw new InvalidOperationException("This transaction has already been committed or rolled back.");

            await _commit(cancellationToken).ConfigureAwait(false);
            MonjoTransactionContext.Clear();
        }

        /// <summary>Rolls back with the provider-native rollback. Throws <see cref="InvalidOperationException"/> when already completed.</summary>
        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _state, StateRolledBack) != StateOpen)
                throw new InvalidOperationException("This transaction has already been committed or rolled back.");

            await _rollback(cancellationToken).ConfigureAwait(false);
            MonjoTransactionContext.Clear();
        }

        /// <summary>Rolls back when the transaction is still open, then releases the native resources.</summary>
        public async ValueTask DisposeAsync()
        {
            if (_state == StateOpen
                && Interlocked.CompareExchange(ref _state, StateRolledBack, StateOpen) == StateOpen)
            {
                await _rollback(CancellationToken.None).ConfigureAwait(false);
            }

            await _disposeNative().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Ambient transaction scope. Uses <see cref="AsyncLocal{T}"/> (the same isolation model the
    /// application already uses for <c>CurrentRequestContext</c>): no locks, no static mutable
    /// state, correct isolation across concurrent async flows.
    /// </summary>
    public static class MonjoTransactionContext
    {
        private static readonly AsyncLocal<MonjoTransaction?> _current = new();

        /// <summary>The transaction ambient in the current async scope, or <c>null</c>.</summary>
        public static MonjoTransaction Current => _current.Value;

        internal static void Set(MonjoTransaction transaction) => _current.Value = transaction;

        internal static void Clear() => _current.Value = null;
    }
}
