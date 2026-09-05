namespace Monjo
{
    /// <summary>
    /// A database provider (MongoDB / PostgreSQL / SQLite / future). Registered once at startup,
    /// resolved once, shared by every repository. A provider owns its native client/pool and
    /// keeps every provider-specific dependency inside its own package.
    /// </summary>
    public interface IMonjoProvider
    {
        /// <summary>Canonical provider name: "MongoDB", "PostgreSQL" or "SQLite".</summary>
        string Name { get; }

        /// <summary>The provider's connection (singleton, thread-safe).</summary>
        IMonjoConnection Connection { get; }

        /// <summary>Idempotently prepares the entity (schema/indexes) — see <see cref="IMonjoConnection.EnsureEntityReadyAsync{T}"/>.</summary>
        Task EnsureEntityReadyAsync<T>(CancellationToken cancellationToken = default) where T : class;

        /// <summary>Starts a provider-native transaction (see <see cref="IMonjoConnection.BeginTransactionAsync"/>).</summary>
        Task<MonjoTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    }
}
