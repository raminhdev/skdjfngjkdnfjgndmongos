using MongoDB.Driver;

namespace Utilities.MongoDatabase.Extensions
{
    public static class MongoDatabaseExtensions
    {
        public static bool ContainsCollection(this IMongoDatabase db, string collectionName) =>
            db.ListCollectionNames().ToList()
            .Any(_collectionName => _collectionName == collectionName);

        public static async Task<bool> ContainsCollectionAsync(this IMongoDatabase db, string collectionName) =>
            (await (await db.ListCollectionNamesAsync()).ToListAsync())
            .Any(_collectionName => _collectionName == collectionName);
    }
}
