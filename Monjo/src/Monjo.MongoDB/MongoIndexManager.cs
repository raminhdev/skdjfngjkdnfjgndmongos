using MongoDB.Bson;
using MongoDB.Driver;
using Monjo.Metadata;

namespace Monjo.MongoDB
{
    /// <summary>
    /// Creates indexes declared via <c>[MonjoIndex]</c> idempotently, once per entity per process
    /// (gated by <see cref="EntityReadinessGate"/>; existing indexes are listed once and matched
    /// by name). Never runs per request.
    /// </summary>
    internal static class MongoIndexManager
    {
        public static Task EnsureIndexesAsync<T>(IMongoDatabase database, bool autoCreateIndexes) where T : class
        {
            if (!autoCreateIndexes)
                return Task.CompletedTask;

            var tableName = MongoMonjoConnection.GetTableName(typeof(T));
            // The key includes the database: index creation belongs to a specific database.
            return EntityReadinessGate.EnsureAsync(
                "MongoDB:" + database.DatabaseNamespace.DatabaseName + ":" + tableName,
                token => EnsureIndexesCoreAsync<T>(database, token));
        }

        private static async Task EnsureIndexesCoreAsync<T>(IMongoDatabase database, CancellationToken cancellationToken) where T : class
        {
            var tableName = MongoMonjoConnection.GetTableName(typeof(T));
            var meta = MonjoEntityMetadata.Get<T>();
            if (meta.Indexes.Count == 0)
                return;

            var collection = database.GetCollection<T>(tableName);

            var existing = new HashSet<string>(StringComparer.Ordinal);
            await foreach (var index in collection.Indexes.ListAsync(cancellationToken).ConfigureAwait(false))
                existing.Add(index.Name);

            foreach (var index in meta.Indexes)
            {
                if (existing.Contains(index.Name))
                    continue;

                var keys = new BsonDocument();
                foreach (var column in index.Columns)
                {
                    var columnMeta = meta.FindColumn(column.Property)
                        ?? throw new MonjoException($"Index '{index.Name}' references unknown column '{column.Property}'.");
                    keys[columnMeta.ColumnName] = column.Descending ? -1 : 1;
                }

                var model = new CreateIndexModel<T>(
                    new IndexKeysDefinition<T>(keys),
                    new CreateIndexOptions
                    {
                        Name = index.Name,
                        Unique = index.Unique,
                    });

                await collection.Indexes.CreateOneAsync(model, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
