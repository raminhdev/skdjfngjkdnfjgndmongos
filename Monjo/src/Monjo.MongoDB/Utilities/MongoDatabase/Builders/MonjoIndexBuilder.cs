using System.Linq.Expressions;
using MongoDB.Driver;
using Utilities.MongoDatabase.Contracts;
using Utilities.MongoDatabase.Documents;

namespace Utilities.MongoDatabase.Builders
{
    public class MonjoIndexBuilder<TDocument>(
        IMonjoRepository<TDocument> monjoRepository,
        IndexKeysDefinition<TDocument> indexKeysDefinition)
        : IMonjoIndexBuilder<TDocument>
        where TDocument : BaseDocument
    {
        private IndexKeysDefinition<TDocument> _indexBuilder = indexKeysDefinition;

        public IMonjoIndexBuilder<TDocument> AscendingIndex(Expression<Func<TDocument, object>> filterExpression)
        {
            _indexBuilder = _indexBuilder.Ascending(filterExpression);

            return this;
        }

        public IMonjoIndexBuilder<TDocument> DescendingIndex(Expression<Func<TDocument, object>> filterExpression)
        {
            _indexBuilder = _indexBuilder.Descending(filterExpression);

            return this;
        }

        public void Build(CreateIndexOptions options = null)
        {
            var model = new CreateIndexModel<TDocument>(_indexBuilder, options);
            monjoRepository.CreateIndexOne(model);
        }

        public async Task BuildAsync(CreateIndexOptions options = null)
        {
            var model = new CreateIndexModel<TDocument>(_indexBuilder, options);
            await monjoRepository.CreateIndexOneAsync(model);
        }
    }
}
