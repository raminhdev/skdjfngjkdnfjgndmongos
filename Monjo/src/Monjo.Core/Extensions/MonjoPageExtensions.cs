using Utilities.MongoDatabase.Filter;

namespace Utilities.MongoDatabase.Extensions
{
    /// <summary>Applies <see cref="MonjoPage"/> to a queryable (pure LINQ; provider-agnostic).</summary>
    public static class MonjoPageExtensions
    {
        public static IQueryable<T> Apply<T>(this MonjoPage monjoPage, IQueryable<T> query)
        {
            if (monjoPage != null)
                query = query.Skip((monjoPage.Index - 1) * monjoPage.Size).Take(monjoPage.Size);
            return query;
        }
    }
}
