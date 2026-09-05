using System.Collections.Concurrent;
using System.Data.Common;
using Monjo;

namespace Monjo.Sql
{
    /// <summary>
    /// The SQL provider's <see cref="IMonjoConnection"/>. Stateless and thread-safe: it holds a
    /// cached repository per entity type and delegates to the provider's pooled connections.
    /// No physical connection is opened at construction — the pool does that on first use.
    /// </summary>
    public sealed class SqlMonjoConnection : IMonjoConnection
    {
        private readonly SqlMonjoProvider _provider;
        private readonly ConcurrentDictionary<Type, object> _repositories = new();

        /// <summary>
        /// Per-type readiness state (gate key + work delegate), built once per type. After the
        /// first call for a type, the hot path of every repository operation costs two
        /// dictionary lookups and an IsCompleted check — no allocations (no per-call key
        /// string, lambda or closure).
        /// </summary>
        private readonly ConcurrentDictionary<Type, (string Key, Func<CancellationToken, Task> Work)> _readiness = new();

        internal SqlMonjoConnection(SqlMonjoProvider provider)
        {
            _provider = provider;
        }

        public string ProviderName => _provider.Name;
        public string DatabaseName => _provider.DatabaseName;

        /// <summary>The provider dialect — used by operation contexts to convert bound parameter values.</summary>
        internal SqlDialect Dialect => _provider.Dialect;

        public IMonjoRepository<T> CreateRepository<T>() where T : class
            => (IMonjoRepository<T>)_repositories.GetOrAdd(typeof(T), _ => _provider.CreateRepositoryCore<T>());

        public Task EnsureEntityReadyAsync<T>(CancellationToken cancellationToken = default) where T : class
        {
            if (_readiness.TryGetValue(typeof(T), out var readiness))
                return EntityReadinessGate.EnsureAsync(readiness.Key, readiness.Work, cancellationToken);

            // First call for this type: builds (and caches) the SQL plan — mapping errors surface
            // here, not mid-query — and caches the one-time work for the readiness gate.
            var meta = _provider.GetMetadata<T>();
            // The key identifies the concrete database (file for SQLite, DB name for PostgreSQL):
            // DDL belongs to a specific database, never a table name alone.
            var key = _provider.Name + ":" + _provider.DatabaseIdentity + ":" + meta.Core.TableName;
            var work = new Func<CancellationToken, Task>(token => EnsureWorkAsync<T>(meta, token));
            _readiness[typeof(T)] = (key, work);
            return EntityReadinessGate.EnsureAsync(key, work, cancellationToken);
        }

        private async Task EnsureWorkAsync<T>(SqlEntityMetadata meta, CancellationToken cancellationToken)
        {
            await _provider.EnsureProviderPragmasAsync(cancellationToken).ConfigureAwait(false);

            if (_provider.Options.AutoCreateSchema)
                await _provider.ExecuteDdlAsync(meta.CreateSchemaSql, cancellationToken).ConfigureAwait(false);

            if (_provider.Options.AutoCreateIndexes)
                foreach (var index in meta.Core.Indexes)
                    await _provider.ExecuteDdlAsync(meta.BuildIndexSql(index), cancellationToken).ConfigureAwait(false);
        }

        public async Task<MonjoTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            // A transaction gets ONE dedicated pooled connection for its whole lifetime;
            // repository operations inside the ambient scope reuse it (no pool churn).
            var connection = _provider.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            DbTransaction transaction;
            try
            {
                transaction = connection.BeginTransaction();
            }
            catch
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            return new MonjoTransaction(
                native: new SqlTransactionBridge(connection, transaction, _provider.Dialect),
                commit: token => transaction.CommitAsync(token),
                rollback: token => transaction.RollbackAsync(token),
                disposeNative: () =>
                {
                    connection.Dispose();
                    return ValueTask.CompletedTask;
                });
        }
    }

    /// <summary>
    /// Base class for the SQL providers (PostgreSQL, SQLite). Owns the dialect, options,
    /// per-type SQL metadata cache and the shared connection. Derived classes add the
    /// provider-specific dialect and name only.
    /// </summary>
    public abstract class SqlMonjoProvider : IMonjoProvider
    {
        private readonly ConcurrentDictionary<Type, SqlEntityMetadata> _metadata = new();

        public MonjoOptions Options { get; }
        protected internal SqlDialect Dialect { get; }
        public IMonjoConnection Connection { get; }

        /// <summary>Canonical provider name ("PostgreSQL" / "SQLite").</summary>
        public abstract string Name { get; }

        /// <summary>
        /// Identity of the concrete database this provider talks to (used as part of the
        /// one-time DDL gate key). PostgreSQL: the database name; SQLite: the connection string,
        /// since the file path IS the database.
        /// </summary>
        internal virtual string DatabaseIdentity => DatabaseName;

        /// <summary>Command timeout applied to every operation.</summary>
        public int CommandTimeoutSeconds { get; protected set; }

        public string DatabaseName => Options.DatabaseName ?? string.Empty;

        protected SqlMonjoProvider(MonjoOptions options, SqlDialect dialect, int commandTimeoutSeconds)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
            CommandTimeoutSeconds = commandTimeoutSeconds;
            Connection = new SqlMonjoConnection(this);
        }

        public Task EnsureEntityReadyAsync<T>(CancellationToken cancellationToken = default) where T : class
            => Connection.EnsureEntityReadyAsync<T>(cancellationToken);

        public Task<MonjoTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Connection.BeginTransactionAsync(cancellationToken);

        internal DbConnection CreateConnection() => Dialect.CreateConnection(Options);

        internal SqlEntityMetadata GetMetadata<T>()
            => _metadata.GetOrAdd(typeof(T), _ => SqlEntityMetadata.Build(this, typeof(T)));

        internal SqlMonjoRepository<T> CreateRepositoryCore<T>() where T : class
            => new(this, (SqlMonjoConnection)Connection);

        internal async Task ExecuteDdlAsync(string sql, CancellationToken cancellationToken)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Provider-level, once-per-process initialization (e.g. SQLite WAL pragmas).
        /// Must be idempotent and cheap. Default: nothing.
        /// </summary>
        protected internal virtual Task EnsureProviderPragmasAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
