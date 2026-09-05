using System.Linq.Expressions;
using MongoDB.Driver.Linq;
using Utilities.Models.Updates;
using Utilities.MongoDatabase.Filter;
using Utilities.Utilities;

namespace Utilities.MongoDatabase.Extensions
{
    public static class IQueryableExtensions
    {
        public static IQueryable<T> Apply<T>(this IQueryable<T> query, IList<IList<MonjoCondition>> monjoConditions,
            string collectionName)
        {
            return monjoConditions.Apply(query, collectionName);
        }

        public static IQueryable<T> Apply<T>(this IQueryable<T> query, IList<MonjoOrder> monjoOrders,
            string collectionName)
        {
            return monjoOrders.Apply(query, collectionName);
        }

        public static IQueryable<T> Apply<T>(this IQueryable<T> query, IList<IList<MonjoCondition>> monjoConditions)
        {
            return monjoConditions.Apply(query, typeof(T).Name);
        }

        public static IQueryable<T> Apply<T>(this IQueryable<T> query, IList<MonjoOrder> monjoOrders)
        {
            return monjoOrders.Apply(query, typeof(T).Name);
        }

        public static IQueryable<T> Apply<T>(this IQueryable<T> query, MonjoPage monjoPage)
        {
            return monjoPage.Apply(query);
        }

        public static MonjoFilteredResult<T> Execute<T>(this IQueryable<T> query, MonjoQuery monjoQuery)
        {
            return Execute(query, monjoQuery, typeof(T).Name);
        }

        public static MonjoFilteredResult<T> Execute<T>(this IQueryable<T> query, MonjoQuery monjoQuery,
            string collectionName)
        {
            query = query
                .Apply(monjoQuery.Where, collectionName)
                .Apply(monjoQuery.Order, collectionName);

            var totalCount = query.Count();
            var pageSize = monjoQuery.Page?.Size ?? totalCount;
            var pageCount = (int)Math.Ceiling(totalCount / (double)pageSize);

            query = query.Apply(monjoQuery.Page);

            var data = query.ToList();

            var result = new MonjoFilteredResult<T>
            {
                TotalCount = totalCount,
                PageCount = pageCount,
                Data = data
            };

            return result;
        }

        public static MonjoFilteredResult<T> Execute<T>(this IQueryable<T> query, MonjoPage monjoPage)
        {
            return query.Execute(new MonjoQuery { Page = monjoPage }, null);
        }

        public static async Task<MonjoFilteredResult<T>> ExecuteAsync<T>(
            this IQueryable<T> query,
            MonjoQuery monjoQuery,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(query, monjoQuery, typeof(T).Name, cancellationToken);
        }

        public static async Task<MonjoFilteredResult<T>> ExecuteAsync<T>(
            this IQueryable<T> query,
            MonjoQuery monjoQuery,
            string collectionName,
            CancellationToken cancellationToken = default)
        {
            var filtered = query.Apply(monjoQuery.Where, collectionName);

            var totalCount = await filtered.CountAsync(cancellationToken);

            var data = await filtered
                .Apply(monjoQuery.Order, collectionName)
                .Apply(monjoQuery.Page)
                .ToListAsync(cancellationToken);

            query
                .Apply(monjoQuery.Where, collectionName)
                .Apply(monjoQuery.Order, collectionName);

            // var totalCount = await query.CountAsync(cancellationToken);

            var pageSize = monjoQuery.Page?.Size ?? totalCount;
            var pageCount = (int)Math.Ceiling(totalCount / (double)pageSize);

            // query = query.Apply(monjoQuery.Page);

            // var data = await query.ToListAsync(cancellationToken);

            return new MonjoFilteredResult<T>
            {
                TotalCount = totalCount,
                PageCount = pageCount,
                Data = data
            };
        }


        public static async Task<MonjoFilteredResult<T>> ExecuteAsync<T>(this IQueryable<T> query, MonjoPage monjoPage,
            CancellationToken cancellationToken = default)
        {
            return await query.ExecuteAsync(new MonjoQuery { Page = monjoPage }, cancellationToken);
        }


        public static async Task<ManualPaginationResult<TQuery>> PaginateAsync<TQuery>(
            this IQueryable<TQuery> query,
            int? pageIndex,
            int? pageSize,
            CancellationToken cancellationToken = default)
        {
            int index = (pageIndex is null or <= 0) ? 1 : pageIndex.Value;
            int size = (pageSize is null or <= 0) ? 10 : pageSize.Value;

            var totalCount = await query.CountAsync(cancellationToken);
            var pageCount = (int)Math.Ceiling(totalCount / (double)size);

            var data = await query
                .Skip((index - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            return new ManualPaginationResult<TQuery>
            {
                PageCount = pageCount,
                TotalCount = totalCount,
                Data = data
            };
        }


        public static IQueryable<T> ApplyDateFilter<T>(
            this IQueryable<T> query,
            Expression<Func<T, DateTime?>> fieldSelector,
            DateFieldFilter filter)
        {
            if (filter == null)
                return query;

            var value = filter.Operand;

            var param = fieldSelector.Parameters[0];
            var member = fieldSelector.Body;

            Expression body = null;

            switch (filter.Comparison)
            {
                case ComparisonMethods.Equal:
                    if (value.HasValue)
                        body = Expression.Equal(member, Expression.Constant(value, typeof(DateTime?)));
                    break;

                case ComparisonMethods.NotEqual:
                    if (value.HasValue)
                        body = Expression.NotEqual(member, Expression.Constant(value, typeof(DateTime?)));
                    break;

                case ComparisonMethods.GreaterThan:
                    if (value.HasValue)
                        body = Expression.GreaterThan(member, Expression.Constant(value, typeof(DateTime?)));
                    break;

                case ComparisonMethods.GreaterThanOrEqual:
                    if (value.HasValue)
                        body = Expression.GreaterThanOrEqual(member, Expression.Constant(value, typeof(DateTime?)));
                    break;

                case ComparisonMethods.LessThan:
                    if (value.HasValue)
                        body = Expression.LessThan(member, Expression.Constant(value, typeof(DateTime?)));
                    break;

                case ComparisonMethods.LessThanOrEqual:
                    if (value.HasValue)
                        body = Expression.LessThanOrEqual(member, Expression.Constant(value, typeof(DateTime?)));
                    break;

                case ComparisonMethods.IsNull:
                    body = Expression.Equal(member, Expression.Constant(null, typeof(DateTime?)));
                    break;

                case ComparisonMethods.IsNotNull:
                    body = Expression.NotEqual(member, Expression.Constant(null, typeof(DateTime?)));
                    break;

                case ComparisonMethods.IsEmpty:
                    body = Expression.Equal(member, Expression.Constant(null, typeof(DateTime?)));
                    break;

                case ComparisonMethods.IsNotEmpty:
                    body = Expression.NotEqual(member, Expression.Constant(null, typeof(DateTime?)));
                    break;
            }

            if (body == null)
                return query;

            var lambda = Expression.Lambda<Func<T, bool>>(body, param);
            return query.Where(lambda);
        }
    }
}