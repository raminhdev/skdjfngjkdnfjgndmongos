using MongoDB.Driver;
using Utilities.MongoDatabase.Filter;

namespace Utilities.MongoDatabase.Extensions
{
    public static class MonjoPageExtensions
    {
        public static IQueryable<T> Apply<T>(this MonjoPage monjoPage, IQueryable<T> query)
        {
            if (monjoPage != null)
                query = query.Skip((monjoPage.Index - 1) * monjoPage.Size).Take(monjoPage.Size);
            return query;
        }

        public static IFindFluent<T, T> Apply<T>(this MonjoPage monjoPage, IFindFluent<T, T> query)
        {
            if (monjoPage != null)
                query = query.Skip((monjoPage.Index - 1) * monjoPage.Size).Limit(monjoPage.Size);
            return query;
        }
    }
}
