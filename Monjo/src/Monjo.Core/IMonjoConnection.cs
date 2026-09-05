namespace Monjo
{
    /// <summary>
    /// Provider-agnostic database connection. A single instance is created once at startup
    /// (singleton) and shared by every repository; it owns the provider-native client/pool
    /// and must be safe for concurrent use.
    /// </summary>
    public interface IMonjoConnection
    {
        /// <summary>Canonical provider name: "MongoDB", "PostgreSQL" or "SQLite".</summary>
        string ProviderName { get; }

        /// <summary>Database (schema) name taken from configuration.</summary>
        string DatabaseName { get; }

        /// <summary>
        /// Creates (or returns the cached) repository for <typeparamref name="T"/>.
        /// Repository instances are stateless and thread-safe; the result is cached per type.
        /// </summary>
        IMonjoRepository<T> CreateRepository<T>() where T : class;

        /// <summary>
        /// Starts a provider-native transaction and makes it ambient for the current async scope
        /// (see <see cref="MonjoTransactionContext"/>). Repository operations executed inside the
        /// async scope enlist in the transaction automatically.
        /// </summary>
        /// <remarks>
        /// MongoDB transactions require a replica set; on a standalone server this throws
        /// <see cref="MonjoNotSupportedException"/> with an explanatory message.
        /// </remarks>
        Task<MonjoTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Idempotently prepares the entity for use (creates table/indexes when the corresponding
        /// <see cref="MonjoOptions"/> switches are on). Called automatically by repository operations;
        /// the work runs once per entity type per process, never per request.
        /// </summary>
        Task EnsureEntityReadyAsync<T>(CancellationToken cancellationToken = default) where T : class;
    }
}
