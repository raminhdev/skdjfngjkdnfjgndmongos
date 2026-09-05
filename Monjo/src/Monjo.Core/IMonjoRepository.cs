using Utilities.MongoDatabase.Filter;

namespace Monjo
{
    /// <summary>
    /// Provider-independent persistence contract. Implementations are created by a Monjo provider
    /// (MongoDB / PostgreSQL / SQLite) and contain only database-agnostic operations.
    /// </summary>
    /// <remarks>
    /// Mongo-specific operations (pipelines, <c>FilterDefinition</c>, <c>UpdateDefinition</c>, cursors)
    /// are intentionally NOT part of this contract; they remain available on the Mongo provider's
    /// dedicated surface (<c>Utilities.MongoDatabase.IMonjoRepository&lt;T&gt;</c> in the Monjo.MongoDB package).
    /// </remarks>
    public interface IMonjoRepository<T> where T : class
    {
        /// <summary>Physical table/collection name resolved from <c>[MonjoTable]</c> (or <c>[MonjoCollectionName]</c>) / type name.</summary>
        string TableName { get; }

        // ------------------------------------------------------------------ reads

        /// <summary>Fetches a single entity by its identifier (soft-deleted rows are excluded).</summary>
        Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default);

        /// <summary>Returns the first entity matching <paramref name="query"/>, or <c>null</c>. The query's page is ignored; exactly one row is fetched.</summary>
        Task<T?> FindOneAsync(MonjoQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>Returns the entities matching <paramref name="query"/> (page applied when present) without executing a total-count query.</summary>
        Task<IReadOnlyList<T>> FindManyAsync(MonjoQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a full MonjoQuery: filter + order + page, and returns <see cref="MonjoFilteredResult{T}"/>
        /// including <c>TotalCount</c> and <c>PageCount</c>. This is the only common API that executes a count.
        /// Use <see cref="FindManyAsync"/> when the total count is not needed (one round-trip instead of two).
        /// </summary>
        Task<MonjoFilteredResult<T>> QueryAsync(MonjoQuery query, CancellationToken cancellationToken = default);

        /// <summary>Server-side count of matching (non-deleted) entities.</summary>
        Task<long> CountAsync(MonjoQuery? query = null, CancellationToken cancellationToken = default);

        /// <summary>Server-side existence check; fetches at most one row.</summary>
        Task<bool> ExistsAsync(MonjoQuery? query = null, CancellationToken cancellationToken = default);

        // ------------------------------------------------------------------ writes

        /// <summary>Inserts a single entity. Returns the entity (identifier generated when it was null).</summary>
        Task<T> InsertAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>Inserts many entities using a provider-native bulk write where available.</summary>
        Task InsertManyAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>Replaces the full row of an existing (non-deleted) entity by identifier. No-op when the entity does not exist.</summary>
        Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates only the columns set on <paramref name="update"/> for the rows matching <paramref name="filter"/>.
        /// Returns the number of affected rows.
        /// </summary>
        Task<int> UpdateColumnsAsync(MonjoColumnUpdate update, MonjoQuery? filter = null, CancellationToken cancellationToken = default);

        /// <summary>Inserts the entity when its identifier does not exist yet, otherwise updates all columns of the existing row.</summary>
        Task UpsertAsync(T entity, CancellationToken cancellationToken = default);

        // ------------------------------------------------------------------ deletes

        /// <summary>Soft-deletes the entity by identifier (sets <c>IsDeleted</c> + audit fields when the entity model supports them; otherwise hard-deletes).</summary>
        Task DeleteAsync(object id, CancellationToken cancellationToken = default);

        /// <summary>Soft-deletes all rows matching <paramref name="filter"/> (a null filter matches every non-deleted row).</summary>
        Task DeleteManyAsync(MonjoQuery? filter = null, CancellationToken cancellationToken = default);

        /// <summary>Physically deletes the row by identifier.</summary>
        Task HardDeleteAsync(object id, CancellationToken cancellationToken = default);

        /// <summary>Physically deletes all rows matching <paramref name="filter"/> (a null filter matches every row, including soft-deleted ones).</summary>
        Task HardDeleteManyAsync(MonjoQuery? filter = null, CancellationToken cancellationToken = default);
    }
}
