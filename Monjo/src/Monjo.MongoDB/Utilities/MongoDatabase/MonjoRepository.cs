using System.Collections.Concurrent;
using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Utilities.Attributes;
using Utilities.MongoDatabase.Builders;
using Utilities.MongoDatabase.Contracts;
using Utilities.MongoDatabase.Documents;
using Utilities.MongoDatabase.Extensions;
using Utilities.MongoDatabase.Filter;

namespace Utilities.MongoDatabase
{
    /// <summary>
    /// The original Mongo repository, preserved for source and behavioral compatibility.
    /// It now derives from the common <see cref="Monjo.MongoDB.MongoMonjoRepository{TDocument}"/>:
    /// the legacy Mongo surface (FilterDefinition/UpdateDefinition/pipelines/cursors) is kept
    /// exactly as before, and the provider-agnostic <c>Monjo.IMonjoRepository&lt;T&gt;</c>
    /// surface is inherited. Existing applications compile and behave unchanged.
    /// </summary>
    public class MonjoRepository<TDocument> : Monjo.MongoDB.MongoMonjoRepository<TDocument>, IMonjoRepository<TDocument>
        where TDocument : BaseDocument
    {
        private static readonly ConcurrentDictionary<Type, string> _collectionNameCache = new();

        private readonly IMonjoConnection _connection;

        protected readonly IMongoCollection<TDocument> _collection;

        public MonjoRepository(IMonjoConnection connection)
            : base(
                connection as Monjo.MongoDB.MongoMonjoConnection ?? throw new InvalidOperationException(
                    "The injected IMonjoConnection is not a Monjo provider connection. " +
                    "Register the provider with services.UseMonjoMongoDB()."),
                connection.Database.GetCollection<TDocument>(CollectionName))
        {
            _connection = connection;
            _collection = Collection;
            Configure();
        }

        protected virtual void Configure()
        {
        }

        public string CollectionName
        {
            get
            {
                return _collectionNameCache.GetOrAdd(typeof(TDocument), t =>
                    t.GetCustomAttributes(typeof(MonjoCollectionNameAttribute), true)
                        .OfType<MonjoCollectionNameAttribute>()
                        .FirstOrDefault()?.CollectionName ?? t.Name);
            }
        }

        public string IdentifierName
        {
            get => MongoCollectionExtensions.GetIdentifierName<TDocument>();
        }

        public bool CollectionExists()
        {
            return _connection.Database.ContainsCollection(CollectionName);
        }

        public virtual IQueryable<TDocument> AsQueryable()
        {
            return _collection.AsQueryable().Where(t => !t.IsDeleted);
        }

        public virtual async Task<IList<BsonDocument>> AggregateAsync(
            PipelineDefinition<TDocument, BsonDocument> pipeline, CancellationToken ctx = default)
        {
            var asyncCursor = await _collection.AggregateAsync(pipeline, cancellationToken: ctx);

            return await asyncCursor.ToListAsync(cancellationToken: ctx);
        }

        public virtual IEnumerable<TDocument> FilterBy(
            Expression<Func<TDocument, bool>> filterExpression)
        {
            return _collection.Find(CombineExpressionToDefalutFilter(filterExpression)).ToEnumerable();
        }

        public virtual async Task<IList<TDocument>> FilterByAsync(
            Expression<Func<TDocument, bool>> filterExpression, CancellationToken ctx = default)
        {
            return await _collection.Find(CombineExpressionToDefalutFilter(filterExpression))
                .ToListAsync(cancellationToken: ctx);
        }

        public virtual async Task<MonjoFilteredResult<TDocument>> FilterByAsync(MonjoQuery query,
            CancellationToken ctx = default)
        {
            return await _collection.AsQueryable().ExecuteAsync(query, ctx);
        }

        public virtual IEnumerable<TProjected> FilterBy<TProjected>(
            Expression<Func<TDocument, bool>> filterExpression,
            Expression<Func<TDocument, TProjected>> projectionExpression)
        {
            return _collection.Find(CombineExpressionToDefalutFilter(filterExpression)).Project(projectionExpression)
                .ToEnumerable();
        }

        public virtual async Task<IList<TProjected>> FilterByAsync<TProjected>(
            Expression<Func<TDocument, bool>> filterExpression,
            Expression<Func<TDocument, TProjected>> projectionExpression, CancellationToken ctx = default)
        {
            return await _collection.Find(CombineExpressionToDefalutFilter(filterExpression))
                .Project(projectionExpression).ToListAsync(cancellationToken: ctx);
        }

        public long Count(Expression<Func<TDocument, bool>> filterExpression)
        {
            return _collection.CountDocuments(CombineExpressionToDefalutFilter(filterExpression));
        }

        public async Task<long> CountAsync(Expression<Func<TDocument, bool>> filterExpression,
            CancellationToken ctx = default)
        {
            return await _collection.CountDocumentsAsync(CombineExpressionToDefalutFilter(filterExpression),
                cancellationToken: ctx);
        }

        public bool Exists(Expression<Func<TDocument, bool>> filterExpression)
        {
            return AsQueryable().Where(filterExpression).Any();
        }

        public async Task<bool> ExistsAsync(Expression<Func<TDocument, bool>> filterExpression,
            CancellationToken ctx = default)
        {
            return await AsQueryable().Where(filterExpression).AnyAsync(cancellationToken: ctx);
        }

        public virtual IFindFluent<TDocument, TDocument> Find(
            Expression<Func<TDocument, bool>> filterExpression, int batchSize = 500)
        {
            return _collection.Find(CombineExpressionToDefalutFilter(filterExpression), new FindOptions()
            {
                BatchSize = batchSize
            });
        }

        public virtual Task<IAsyncCursor<TDocument>> FindAsync(
            Expression<Func<TDocument, bool>> filterExpression, int batchSize = 500, CancellationToken ctx = default)
        {
            return _collection.FindAsync(CombineExpressionToDefalutFilter(filterExpression),
                new FindOptions<TDocument>()
                {
                    BatchSize = batchSize
                }, ctx);
        }

        public virtual IFindFluent<TDocument, TDocument> Find(FilterDefinition<TDocument> filter)
        {
            return _collection.Find(CombineFilterToDefalutFilterDefinition(filter));
        }

        public virtual TDocument FindOne(Expression<Func<TDocument, bool>> filterExpression)
        {
            return _collection.Find(CombineExpressionToDefalutFilter(filterExpression)).FirstOrDefault();
        }

        public virtual Task<TDocument> FindOneAsync(Expression<Func<TDocument, bool>> filterExpression,
            CancellationToken ctx = default)
        {
            return Find(filterExpression).FirstOrDefaultAsync(ctx);
        }

        public virtual TDocument FindById(object id)
        {
            var filter = Builders<TDocument>.Filter.Eq(IdentifierName, id);
            return _collection.Find(CombineFilterToDefalutFilterDefinition(filter)).SingleOrDefault();
        }

        public virtual async Task<TDocument> FindByIdAsync(object id, CancellationToken ctx = default)
        {
            var filter = Builders<TDocument>.Filter.Eq(IdentifierName, id);
            return await (await _collection.FindAsync(CombineFilterToDefalutFilterDefinition(filter),
                    cancellationToken: ctx))
                .SingleOrDefaultAsync(cancellationToken: ctx);
        }

        public virtual void InsertOne(TDocument document)
        {
            _collection.InsertOne(document);
        }

        public virtual async Task InsertOneAsync(TDocument document, CancellationToken cancellationToken = default)
        {
            await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        }

        public virtual void InsertMany(IEnumerable<TDocument> documents, CancellationToken ctx = default)
        {
            _collection.InsertMany(documents, cancellationToken: ctx);
        }

        public new virtual async Task InsertManyAsync(IEnumerable<TDocument> documents, CancellationToken ctx = default)
        {
            await _collection.InsertManyAsync(documents, cancellationToken: ctx);
        }

        public virtual void ReplaceOne(TDocument document)
        {
            var filter = Builders<TDocument>.Filter.Eq(IdentifierName, document.GetIdentifierValue());

            UpdateDocument(document);
            _collection.FindOneAndReplace(CombineFilterToDefalutFilterDefinition(filter), document);
        }

        public virtual async Task ReplaceOneAsync(TDocument document, CancellationToken ctx = default)
        {
            var filter = Builders<TDocument>.Filter.Eq(IdentifierName, document.GetIdentifierValue());
            UpdateDocument(document);
            await _collection.FindOneAndReplaceAsync(CombineFilterToDefalutFilterDefinition(filter), document,
                cancellationToken: ctx);
        }

        public virtual async Task ReplaceManyAsync(IEnumerable<ReplaceManyInput<TDocument>> replaceManyInputs,
            CancellationToken ctx = default)
        {
            var operations = (from replaceManyInput in replaceManyInputs
                    let filter = replaceManyInput.FilterExpression != null
                        ? Builders<TDocument>.Filter.Where(replaceManyInput.FilterExpression)
                        : Builders<TDocument>.Filter.Eq(IdentifierName, replaceManyInput.Document.Id)
                    select new ReplaceOneModel<TDocument>(filter, replaceManyInput.Document)).Cast<WriteModel<TDocument>>()
                .ToList();

            if (operations.Count != 0)
                await _collection.BulkWriteAsync(operations, new BulkWriteOptions { IsOrdered = false }, ctx);
        }

        public Task BulkWriteAsync(IEnumerable<WriteModel<TDocument>> requests, CancellationToken ctx = default)
        {
            return _collection.BulkWriteAsync(requests, cancellationToken: ctx);
        }


        public virtual void DeleteMany(Expression<Func<TDocument, bool>> filterExpression)
        {
            var update = CreateDeleteUpdate();
            _collection.UpdateMany(CombineExpressionToDefalutFilter(filterExpression), update);
        }

        public virtual async Task DeleteManyAsync(Expression<Func<TDocument, bool>> filterExpression,
            CancellationToken ctx = default)
        {
            var update = CreateDeleteUpdate();
            await _collection.UpdateManyAsync(CombineExpressionToDefalutFilter(filterExpression), update,
                cancellationToken: ctx);
        }

        public virtual void DeleteOne(Expression<Func<TDocument, bool>> filterExpression)
        {
            var update = CreateDeleteUpdate();
            _collection.FindOneAndUpdate(CombineExpressionToDefalutFilter(filterExpression), update);
        }

        public virtual async Task DeleteOneAsync(Expression<Func<TDocument, bool>> filterExpression,
            CancellationToken ctx = default)
        {
            var update = CreateDeleteUpdate();
            await _collection.FindOneAndUpdateAsync(CombineExpressionToDefalutFilter(filterExpression), update,
                cancellationToken: ctx);
        }

        public virtual void DeleteById(object id)
        {
            var filter = Builders<TDocument>.Filter.Eq(IdentifierName, id);
            var update = CreateDeleteUpdate();
            _collection.FindOneAndUpdate(CombineFilterToDefalutFilterDefinition(filter), update);
        }

        public virtual async Task DeleteByIdAsync(object id, CancellationToken ctx = default)
        {
            var filter = Builders<TDocument>.Filter.Eq(IdentifierName, id);
            var update = CreateDeleteUpdate();
            await _collection.FindOneAndUpdateAsync(CombineFilterToDefalutFilterDefinition(filter), update,
                cancellationToken: ctx);
        }

        public void CreateIndexOne(CreateIndexModel<TDocument> createIndexModel)
        {
            _collection.Indexes.CreateOne(createIndexModel);
        }

        public async Task CreateIndexOneAsync(CreateIndexModel<TDocument> createIndexModel,
            CancellationToken ctx = default)
        {
            await _collection.Indexes.CreateOneAsync(createIndexModel, cancellationToken: ctx);
        }

        public void CreateIndexMany(IEnumerable<CreateIndexModel<TDocument>> createIndexModels)
        {
            _collection.Indexes.CreateMany(createIndexModels);
        }

        public async Task CreateIndexManyAsync(IEnumerable<CreateIndexModel<TDocument>> createIndexModels,
            CancellationToken ctx = default)
        {
            await _collection.Indexes.CreateManyAsync(createIndexModels, ctx);
        }

        public void DropIndexOne(string name)
        {
            _collection.Indexes.DropOne(name);
        }

        public async Task DropIndexOneAsync(string name, CancellationToken ctx = default)
        {
            await _collection.Indexes.DropOneAsync(name, ctx);
        }

        public void DropIndexAll()
        {
            _collection.Indexes.DropAll();
        }

        public async Task DropIndexAllAsync(CancellationToken ctx = default)
        {
            await _collection.Indexes.DropAllAsync(ctx);
        }

        public IMonjoIndexBuilder<TDocument> AscendingIndex(Expression<Func<TDocument, object>> filterExpression)
        {
            var indexBuilder = new IndexKeysDefinitionBuilder<TDocument>();
            return new MonjoIndexBuilder<TDocument>(this, indexBuilder.Ascending(filterExpression));
        }

        public IMonjoIndexBuilder<TDocument> DescendingIndex(Expression<Func<TDocument, object>> filterExpression)
        {
            var indexBuilder = new IndexKeysDefinitionBuilder<TDocument>();
            return new MonjoIndexBuilder<TDocument>(this, indexBuilder.Descending(filterExpression));
        }

        public async Task<TDocument> FindOneAndUpdateAsync(FilterDefinition<TDocument> filter,
            UpdateDefinition<TDocument> update, CancellationToken ctx = default)
        {
            return await _collection.FindOneAndUpdateAsync(CombineFilterToDefalutFilterDefinition(filter),
                CombineUpdateToDefalutUpdateDefinition(update), cancellationToken: ctx);
        }

        public TDocument FindOneAndUpdate(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update)
        {
            return _collection.FindOneAndUpdate(CombineFilterToDefalutFilterDefinition(filter),
                CombineUpdateToDefalutUpdateDefinition(update));
        }

        public async Task<UpdateResult> UpdateManyAsync(FilterDefinition<TDocument> filter,
            UpdateDefinition<TDocument> update, CancellationToken ctx = default)
        {
            return await _collection.UpdateManyAsync(CombineFilterToDefalutFilterDefinition(filter),
                CombineUpdateToDefalutUpdateDefinition(update), cancellationToken: ctx);
        }

        public async Task<UpdateResult> UpdateManyAsync(FilterDefinition<TDocument> filter,
            UpdateDefinition<TDocument> update, UpdateOptions options = null, CancellationToken ctx = default)
        {
            return await _collection.UpdateManyAsync(CombineFilterToDefalutFilterDefinition(filter),
                CombineUpdateToDefalutUpdateDefinition(update),
                options, ctx);
        }

        public UpdateResult UpdateMany(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update)
        {
            return _collection.UpdateMany(CombineFilterToDefalutFilterDefinition(filter),
                CombineUpdateToDefalutUpdateDefinition(update));
        }

        public UpdateResult UpsertOne(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update)
        {
            return _collection.UpdateOne(CombineFilterToDefalutFilterDefinition(filter),
                CombineUpdateToDefalutUpdateDefinition(update), new UpdateOptions() { IsUpsert = true });
        }

        public async Task<UpdateResult> UpsertOneAsync(FilterDefinition<TDocument> filter,
            UpdateDefinition<TDocument> update, CancellationToken ctx = default)
        {
            return await _collection.UpdateOneAsync(CombineFilterToDefalutFilterDefinition(filter),
                CombineUpdateToDefalutUpdateDefinition(update), new UpdateOptions() { IsUpsert = true }, ctx);
        }

        public UpdateResult UpsertMany(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update)
        {
            return _collection.UpdateMany(CombineFilterToDefalutFilterDefinition(filter),
                CombineUpdateToDefalutUpdateDefinition(update), new UpdateOptions() { IsUpsert = true });
        }

        public async Task<UpdateResult> UpsertManyAsync(FilterDefinition<TDocument> filter,
            UpdateDefinition<TDocument> update, CancellationToken ctx = default)
        {
            return await _collection.UpdateManyAsync(CombineFilterToDefalutFilterDefinition(filter),
                CombineUpdateToDefalutUpdateDefinition(update), new UpdateOptions() { IsUpsert = true }, ctx);
        }

        public async Task<TDocument> FindOneAndUpdateAsync(Expression<Func<TDocument, bool>> filter,
            UpdateDefinition<TDocument> update, CancellationToken ctx = default)
        {
            return await _collection.FindOneAndUpdateAsync(CombineExpressionToDefalutFilter(filter),
                CombineUpdateToDefalutUpdateDefinition(update), cancellationToken: ctx);
        }

        public TDocument FindOneAndUpdate(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update,
            CancellationToken ctx = default)
        {
            return _collection.FindOneAndUpdate(CombineExpressionToDefalutFilter(filter), CombineUpdateToDefalutUpdateDefinition(update), cancellationToken: ctx);
        }

        public async Task<UpdateResult> UpdateManyAsync(Expression<Func<TDocument, bool>> filter,
            UpdateDefinition<TDocument> update, CancellationToken ctx = default)
        {
            return await _collection.UpdateManyAsync(CombineExpressionToDefalutFilter(filter),
                CombineUpdateToDefalutUpdateDefinition(update), cancellationToken: ctx);
        }

        public UpdateResult UpdateMany(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update)
        {
            return _collection.UpdateMany(CombineExpressionToDefalutFilter(filter),
                CombineUpdateToDefalutUpdateDefinition(update));
        }

        public UpdateResult UpsertOne(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update)
        {
            return _collection.UpdateOne(CombineExpressionToDefalutFilter(filter),
                CombineUpdateToDefalutUpdateDefinition(update), new UpdateOptions() { IsUpsert = true });
        }

        public async Task<UpdateResult> UpsertOneAsync(Expression<Func<TDocument, bool>> filter,
            UpdateDefinition<TDocument> update, CancellationToken ctx = default)
        {
            return await _collection.UpdateOneAsync(CombineExpressionToDefalutFilter(filter),
                CombineUpdateToDefalutUpdateDefinition(update), new UpdateOptions() { IsUpsert = true }, ctx);
        }

        public UpdateResult UpsertMany(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update)
        {
            return _collection.UpdateMany(CombineExpressionToDefalutFilter(filter),
                CombineUpdateToDefalutUpdateDefinition(update), new UpdateOptions() { IsUpsert = true });
        }

        public async Task<UpdateResult> UpsertManyAsync(Expression<Func<TDocument, bool>> filter,
            UpdateDefinition<TDocument> update, CancellationToken ctx = default)
        {
            return await _collection.UpdateManyAsync(CombineExpressionToDefalutFilter(filter),
                CombineUpdateToDefalutUpdateDefinition(update), new UpdateOptions() { IsUpsert = true }, ctx);
        }

        public virtual async Task RealDeleteMany(Expression<Func<TDocument, bool>> filterExpression)
        {
            await _collection.DeleteManyAsync(filterExpression);
        }

        public virtual async Task RealDeleteManyAsync(Expression<Func<TDocument, bool>> filterExpression,
            CancellationToken ctx = default)
        {
            await _collection.DeleteManyAsync(filterExpression, ctx);
        }


        private static FilterDefinition<TDocument> CombineExpressionToDefalutFilter(
            Expression<Func<TDocument, bool>> filterExpression)
        {
            Expression<Func<TDocument, bool>> defaultCondition = tDocument => !tDocument.IsDeleted;
            var combinedFilter =
                Builders<TDocument>.Filter.And(defaultCondition, Builders<TDocument>.Filter.Where(filterExpression));
            return combinedFilter;
        }

        private static FilterDefinition<TDocument> CombineFilterToDefalutFilterDefinition(
            FilterDefinition<TDocument> filter)
        {
            var defaultFilter = Builders<TDocument>.Filter.Eq(q => q.IsDeleted, false);
            FilterDefinition<TDocument> combinedFilter = defaultFilter & filter;
            return combinedFilter;
        }

        private static UpdateDefinition<TDocument> CombineUpdateToDefalutUpdateDefinition(
            UpdateDefinition<TDocument> update)
        {
            var actor = Monjo.MonjoActorContext.Current;

            return update
                .Set(q => q.ModifiedMoment, DateTime.UtcNow)
                .Set(q => q.ModifiedBy, actor.PublicKey ?? "system")
                .Set(q => q.ModifiedByInfo, actor.DisplayInfo ?? "system : system");
        }

        private UpdateDefinition<TDocument> CreateDeleteUpdate()
        {
            var actor = Monjo.MonjoActorContext.Current;

            return Builders<TDocument>.Update
                .Set(q => q.IsDeleted, true)
                .Set(q => q.DeletedMoment, DateTime.UtcNow)
                .Set(q => q.DeletedBy, actor.PublicKey ?? "system")
                .Set(q => q.DeletedByInfo, actor.DisplayInfo ?? "system : system");
        }

        private void UpdateDocument(TDocument document)
        {
            var actor = Monjo.MonjoActorContext.Current;

            document.ModifiedMoment = DateTime.UtcNow;
            document.ModifiedByInfo = actor.DisplayInfo ?? "system : system";
            document.ModifiedBy = actor.PublicKey ?? "system";
        }
    }

    public class ReplaceManyInput<TDocument>
    {
        public TDocument Document { get; set; }
        public Expression<Func<TDocument, bool>> FilterExpression { get; set; }
    }
}
