using System.Linq.Expressions;
using MongoDB.Driver;

namespace Utilities.MongoDatabase.Contracts
{
    /// <summary>Fluent Mongo index builder (provider-specific capability; the common index concept is <c>Monjo.MonjoIndexDefinition</c>).</summary>
    public interface IMonjoIndexBuilder<TDocument>
    {
        IMonjoIndexBuilder<TDocument> AscendingIndex(Expression<Func<TDocument, object>> filterExpression);

        IMonjoIndexBuilder<TDocument> DescendingIndex(Expression<Func<TDocument, object>> filterExpression);

        void Build(CreateIndexOptions options = null);

        Task BuildAsync(CreateIndexOptions options = null);
    }
}
