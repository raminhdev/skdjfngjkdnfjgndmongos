using System.Linq.Expressions;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Monjo.Metadata;
using Utilities.MongoDatabase.Filter;

namespace Monjo.MongoDB
{
    /// <summary>
    /// The MongoDB implementation of the common <see cref="IMonjoRepository{T}"/>.
    /// Uses native driver operations (Find/CountDocuments/Replace/UpdateMany/Delete) with
    /// server-side filtering, sorting and pagination — no data is materialized before filtering.
    /// Operations automatically enlist in an ambient <see cref="MonjoTransaction"/> when present.
    /// </summary>
    public class MongoMonjoRepository<T> : IMonjoRepository<T> where T : class
    {
        private readonly MongoMonjoConnection _connection;

        /// <summary>The native collection handle (cached, thread-safe). Exposed for the legacy subclass.</summary>
        protected IMongoCollection<T> Collection { get; }

        public string TableName => MongoMonjoConnection.GetTableName(typeof(T));

        public MongoMonjoRepository(MongoMonjoConnection connection, IMongoCollection<T> collection)
        {
            _connection = connection;
            Collection = collection;
        }

        /// <summary>Inside an ambient transaction the driver operations must run on the session.</summary>
        private IMongoCollection<T> ActiveCollection()
        {
            var transaction = MonjoTransactionContext.Current;
            return transaction?.Native is MongoTransactionBridge bridge
                ? Collection.WithSession(bridge.Session)
                : Collection;
        }

        // ------------------------------------------------------------------ reads

        public async Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(id);
            await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);

            var filter = MongoQueryTranslator.CombineWithSoftDelete<T>(MongoQueryTranslator.BuildIdFilter<T>(id));
            using var cursor = await ActiveCollection().FindAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
            return await cursor.SingleOrDefaultAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task<T?> FindOneAsync(MonjoQuery? query = null, CancellationToken cancellationToken = default)
        {
            await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);

            var filter = MongoQueryTranslator.CombineWithSoftDelete<T>(MongoQueryTranslator.BuildWhere<T>(query?.Where));
            var sort = MongoQueryTranslator.BuildSort<T>(query?.Order);

            var fluent = ActiveCollection().Find(filter);
            if (sort is not null)
                fluent = fluent.Sort(sort);

            return await fluent.Limit(1).FirstOrDefaultAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<T>> FindManyAsync(MonjoQuery? query = null, CancellationToken cancellationToken = default)
        {
            await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);

            var filter = MongoQueryTranslator.CombineWithSoftDelete<T>(MongoQueryTranslator.BuildWhere<T>(query?.Where));
            var sort = MongoQueryTranslator.BuildSort<T>(query?.Order);

            var fluent = ActiveCollection().Find(filter);
            if (sort is not null)
                fluent = fluent.Sort(sort);

            var page = query?.Page;
            if (page is { Size: > 0 })
            {
                fluent = fluent.Skip(Math.Max(0, page.Index - 1) * page.Size).Limit(page.Size);
            }

            return await fluent.ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task<MonjoFilteredResult<T>> QueryAsync(MonjoQuery query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);

            var filter = MongoQueryTranslator.CombineWithSoftDelete<T>(MongoQueryTranslator.BuildWhere<T>(query.Where));
            var sort = MongoQueryTranslator.BuildSort<T>(query.Order);

            var totalCount = await ActiveCollection().CountDocumentsAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);

            var fluent = ActiveCollection().Find(filter);
            if (sort is not null)
                fluent = fluent.Sort(sort);

            var page = query.Page;
            var pageSize = 0;
            if (page is { Size: > 0 })
            {
                pageSize = page.Size;
                fluent = fluent.Skip(Math.Max(0, page.Index - 1) * page.Size).Limit(page.Size);
            }

            var data = await fluent.ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (pageSize == 0)
                pageSize = data.Count > 0 ? data.Count : 1;

            return new MonjoFilteredResult<T>
            {
                TotalCount = totalCount,
                PageCount = (int)Math.Ceiling(totalCount / (double)pageSize),
                Data = data
            };
        }

        public async Task<long> CountAsync(MonjoQuery? query = null, CancellationToken cancellationToken = default)
        {
            await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);
            var filter = MongoQueryTranslator.CombineWithSoftDelete<T>(MongoQueryTranslator.BuildWhere<T>(query?.Where));
            return await ActiveCollection().CountDocumentsAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task<bool> ExistsAsync(MonjoQuery? query = null, CancellationToken cancellationToken = default)
        {
            await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);
            var filter = MongoQueryTranslator.CombineWithSoftDelete<T>(MongoQueryTranslator.BuildWhere<T>(query?.Where));
            // limit:1 keeps this a single cheap server-side count.
            var count = await ActiveCollection()
                .CountDocumentsAsync(filter, new CountOptions { Limit = 1 }, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return count > 0;
        }

        // ------------------------------------------------------------------ writes

        public async Task<T> InsertAsync(T entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);
            await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);
            EnsureId(entity);
            await ActiveCollection().InsertOneAsync(entity, cancellationToken: cancellationToken).ConfigureAwait(false);
            return entity;
        }

        public async Task InsertManyAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entities);
            await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);

            var batch = entities.ToList();
            foreach (var entity in batch)
                EnsureId(entity);

            // Native bulk write: the driver batches this server-side.
            await ActiveCollection().InsertManyAsync(batch, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);
            await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);
            StampModified(entity);

            // Single write operation (no read round-trip); matches the legacy Replace semantics.
            var filter = MongoQueryTranslator.CombineWithSoftDelete<T>(MongoQueryTranslator.BuildIdFilter<T>(GetIdValue(entity)));
            await ActiveCollection().ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> UpdateColumnsAsync(MonjoColumnUpdate update, MonjoQuery? filter = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(update);
            if (update.IsEmpty)
                return 0;

            await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);

            var meta = MonjoEntityMetadata.Get<T>();
            var actor = MonjoActorContext.Current;
            if (meta.ModifiedMoment is { } mm && !update.Contains(mm.ColumnName))
                update.Set(mm.ColumnName, DateTime.UtcNow);
            if (meta.ModifiedBy is { } mb && !update.Contains(mb.ColumnName))
                update.Set(mb.ColumnName, actor.PublicKey ?? "system");
            if (meta.ModifiedByInfo is { } mbi && !update.Contains(mbi.ColumnName))
                update.Set(mbi.ColumnName, actor.DisplayInfo ?? "system : system");

            var updateDefinition = Builders<T>.Update.Combine(update.Values
                .Select(entry =>
                {
                    var column = meta.FindColumn(entry.Key)
                        ?? throw new MonjoException($"Unknown column or property '{entry.Key}' for '{typeof(T).Name}'.");
                    var selector = MongoQueryTranslator.GetSelector<T>(entry.Key);
                    return Builders<T>.Update.Set(selector, entry.Value);
                }));

            var where = MongoQueryTranslator.CombineWithSoftDelete<T>(MongoQueryTranslator.BuildWhere<T>(filter?.Where));
            var result = await ActiveCollection().UpdateManyAsync(where, updateDefinition, cancellationToken: cancellationToken).ConfigureAwait(false);
            return (int)result.ModifiedCount;
        }

        public async Task UpsertAsync(T entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);
            await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);
            EnsureId(entity);
            StampModified(entity);

            // Filter is identifier-only on purpose: a soft-deleted row is replaced (revived) rather than duplicated.
            var filter = MongoQueryTranslator.BuildIdFilter<T>(GetIdValue(entity));
            await ActiveCollection().ReplaceOneAsync(
                filter, entity, new ReplaceOptions { IsUpsert = true }, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        // ------------------------------------------------------------------ deletes

        public async Task DeleteAsync(object id, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(id);
            await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);

            var meta = MonjoEntityMetadata.Get<T>();
            if (meta.IsDeleted is null)
            {
                await ActiveCollection().DeleteOneAsync(MongoQueryTranslator.BuildIdFilter<T>(id), cancellationToken: cancellationToken).ConfigureAwait(false);
                return;
            }

            var filter = MongoQueryTranslator.CombineWithSoftDelete<T>(MongoQueryTranslator.BuildIdFilter<T>(id));
            var update = BuildDeleteUpdate<T>();
            await ActiveCollection().UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task DeleteManyAsync(MonjoQuery? filter = null, CancellationToken cancellationToken = default)
        {
            await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);

            var meta = MonjoEntityMetadata.Get<T>();
            if (meta.IsDeleted is null)
            {
                await HardDeleteManyAsync(filter, cancellationToken).ConfigureAwait(false);
                return;
            }

            var where = MongoQueryTranslator.CombineWithSoftDelete<T>(MongoQueryTranslator.BuildWhere<T>(filter?.Where));
            var update = BuildDeleteUpdate<T>();
            await ActiveCollection().UpdateManyAsync(where, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task HardDeleteAsync(object id, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(id);
            await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);
            await ActiveCollection().DeleteOneAsync(MongoQueryTranslator.BuildIdFilter<T>(id), cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task HardDeleteManyAsync(MonjoQuery? filter = null, CancellationToken cancellationToken = default)
        {
            await _connection.EnsureEntityReadyAsync<T>(cancellationToken).ConfigureAwait(false);

            // Hard delete targets every document (including soft-deleted ones) matching the filter.
            var where = MongoQueryTranslator.BuildWhere<T>(filter?.Where);
            await ActiveCollection().DeleteManyAsync(where, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        // ------------------------------------------------------------------ infrastructure

        private static void EnsureId(T entity)
        {
            var meta = MonjoEntityMetadata.Get<T>();
            var id = meta.Id;
            if (id is null)
                return;

            if (id.Property.GetValue(entity) is null)
            {
                // Entities whose id carries [BsonId] (e.g. the legacy BaseDocument with ObjectId
                // representation) let the driver auto-generate; all others get a "N"-format Guid string.
                if (id.Property.GetCustomAttribute<BsonIdAttribute>() is null)
                {
                    var generated = id.NonNullableType == typeof(Guid)
                        ? Guid.NewGuid()
                        : Guid.NewGuid().ToString("N");
                    id.Property.SetValue(entity, generated);
                }
            }
        }

        private static object GetIdValue(T entity)
            => MonjoEntityMetadata.Get<T>().Id!.Property.GetValue(entity)!;

        private static void StampModified(T entity)
        {
            var meta = MonjoEntityMetadata.Get<T>();
            var actor = MonjoActorContext.Current;
            if (meta.ModifiedMoment is { } mm) mm.Property.SetValue(entity, DateTime.UtcNow);
            if (meta.ModifiedBy is { } mb) mb.Property.SetValue(entity, actor.PublicKey ?? "system");
            if (meta.ModifiedByInfo is { } mbi) mbi.Property.SetValue(entity, actor.DisplayInfo ?? "system : system");
        }

        private static UpdateDefinition<T> BuildDeleteUpdate<T>()
        {
            var meta = MonjoEntityMetadata.Get<T>();
            var actor = MonjoActorContext.Current;

            var parts = new List<UpdateDefinition<T>>
            {
                Builders<T>.Update.Set(MongoQueryTranslator.GetSelector<T>(meta.IsDeleted!.Property.Name), true)
            };
            if (meta.DeletedMoment is { } dm) parts.Add(Builders<T>.Update.Set(MongoQueryTranslator.GetSelector<T>(dm.ColumnName), DateTime.UtcNow));
            if (meta.DeletedBy is { } db) parts.Add(Builders<T>.Update.Set(MongoQueryTranslator.GetSelector<T>(db.ColumnName), actor.PublicKey ?? "system"));
            if (meta.DeletedByInfo is { } dbi) parts.Add(Builders<T>.Update.Set(MongoQueryTranslator.GetSelector<T>(dbi.ColumnName), actor.DisplayInfo ?? "system : system"));

            return Builders<T>.Update.Combine(parts);
        }
    }
}
