using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Driver;
using Utilities.MongoDatabase.Documents;
using Utilities.MongoDatabase.Filter;

namespace Utilities.MongoDatabase.Contracts
{
    public interface IMonjoRepository<TDocument>
    {
        IQueryable<TDocument> AsQueryable();

        Task<IList<BsonDocument>> AggregateAsync(PipelineDefinition<TDocument, BsonDocument> pipeline, CancellationToken ctx = default);
        TDocument FindOneAndUpdate(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update);
        UpdateResult UpdateMany(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update);
        UpdateResult UpsertOne(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update);
        UpdateResult UpsertMany(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update);
        TDocument FindOneAndUpdate(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update, CancellationToken ctx = default);
        UpdateResult UpdateMany(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update);
        UpdateResult UpsertOne(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update);
        UpdateResult UpsertMany(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update);
        Task<TDocument> FindOneAndUpdateAsync(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update, CancellationToken ctx = default);
        Task<UpdateResult> UpdateManyAsync(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update, CancellationToken ctx = default);
        Task<UpdateResult> UpdateManyAsync(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update, UpdateOptions options = null, CancellationToken ctx = default);
        Task<UpdateResult> UpsertOneAsync(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update, CancellationToken ctx = default);
        Task<UpdateResult> UpsertManyAsync(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update, CancellationToken ctx = default);
        Task<TDocument> FindOneAndUpdateAsync(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update, CancellationToken ctx = default);
        Task<UpdateResult> UpdateManyAsync(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update, CancellationToken ctx = default);
        Task<UpdateResult> UpsertOneAsync(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update, CancellationToken ctx = default);
        Task<UpdateResult> UpsertManyAsync(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update, CancellationToken ctx = default);
        IEnumerable<TDocument> FilterBy(
            Expression<Func<TDocument, bool>> filterExpression);
        Task<IList<TDocument>> FilterByAsync(
            Expression<Func<TDocument, bool>> filterExpression, CancellationToken ctx = default);
        Task<MonjoFilteredResult<TDocument>> FilterByAsync(MonjoQuery query, CancellationToken ctx = default);
        IEnumerable<TProjected> FilterBy<TProjected>(
            Expression<Func<TDocument, bool>> filterExpression,
            Expression<Func<TDocument, TProjected>> projectionExpression);
        Task<IList<TProjected>> FilterByAsync<TProjected>(
            Expression<Func<TDocument, bool>> filterExpression,
            Expression<Func<TDocument, TProjected>> projectionExpression, CancellationToken ctx = default);
        long Count(Expression<Func<TDocument, bool>> filterExpression);

        Task<long> CountAsync(Expression<Func<TDocument, bool>> filterExpression, CancellationToken ctx = default);

        bool Exists(Expression<Func<TDocument, bool>> filterExpression);

        Task<bool> ExistsAsync(Expression<Func<TDocument, bool>> filterExpression, CancellationToken ctx = default);


        IFindFluent<TDocument, TDocument> Find(Expression<Func<TDocument, bool>> filterExpression, int batchSize = 500);

        Task<IAsyncCursor<TDocument>> FindAsync(Expression<Func<TDocument, bool>> filterExpression, int batchSize = 500, CancellationToken ctx = default);

        //IFindFluent<BsonDocument, BsonDocument> Find(FilterDefinition<BsonDocument> filter);

        IFindFluent<TDocument, TDocument> Find(FilterDefinition<TDocument> filter);

        TDocument FindOne(Expression<Func<TDocument, bool>> filterExpression);

        Task<TDocument> FindOneAsync(Expression<Func<TDocument, bool>> filterExpression, CancellationToken ctx = default);

        TDocument FindById(object id);

        Task<TDocument> FindByIdAsync(object id, CancellationToken ctx = default);

        void InsertOne(TDocument document);

        Task InsertOneAsync(TDocument document,CancellationToken cancellationToken = default);

        void InsertMany(IEnumerable<TDocument> documents, CancellationToken ctx = default);

        Task InsertManyAsync(IEnumerable<TDocument> documents, CancellationToken ctx = default);

        void ReplaceOne(TDocument document);

        Task ReplaceOneAsync(TDocument document, CancellationToken ctx = default);

        Task ReplaceManyAsync(IEnumerable<ReplaceManyInput<TDocument>> replaceManyInputs, CancellationToken ctx = default);

        Task BulkWriteAsync(IEnumerable<WriteModel<TDocument>> requests, CancellationToken ctx = default);

        void DeleteOne(Expression<Func<TDocument, bool>> filterExpression);

        Task DeleteOneAsync(Expression<Func<TDocument, bool>> filterExpression, CancellationToken ctx = default);

        void DeleteById(object id);

        Task DeleteByIdAsync(object id, CancellationToken ctx = default);

        void DeleteMany(Expression<Func<TDocument, bool>> filterExpression);

        Task DeleteManyAsync(Expression<Func<TDocument, bool>> filterExpression, CancellationToken ctx = default);


        void CreateIndexOne(CreateIndexModel<TDocument> createIndexModel);

        Task CreateIndexOneAsync(CreateIndexModel<TDocument> createIndexModel, CancellationToken ctx = default);

        void CreateIndexMany(IEnumerable<CreateIndexModel<TDocument>> createIndexModels);

        Task CreateIndexManyAsync(IEnumerable<CreateIndexModel<TDocument>> createIndexModels, CancellationToken ctx = default);

        void DropIndexOne(string name);
        Task DropIndexOneAsync(string name, CancellationToken ctx = default);

        void DropIndexAll();
        Task DropIndexAllAsync(CancellationToken ctx = default);

        Task RealDeleteMany(Expression<Func<TDocument, bool>> filterExpression);
        Task RealDeleteManyAsync(Expression<Func<TDocument, bool>> filterExpression, CancellationToken ctx = default);

        IMonjoIndexBuilder<TDocument> AscendingIndex(Expression<Func<TDocument, object>> filterExpression);
        IMonjoIndexBuilder<TDocument> DescendingIndex(Expression<Func<TDocument, object>> filterExpression);
    }
}
