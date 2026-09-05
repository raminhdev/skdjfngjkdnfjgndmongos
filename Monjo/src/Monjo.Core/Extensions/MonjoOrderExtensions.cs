using System.Linq.Expressions;
using Utilities.MongoDatabase.Filter;

namespace Utilities.MongoDatabase.Extensions
{
    /// <summary>Translates <see cref="MonjoOrder"/> clauses onto an <see cref="IQueryable{T}"/> (provider-agnostic, pure LINQ).</summary>
    public static class MonjoOrderExtensions
    {
        public static IQueryable<T> Apply<T>(this IList<MonjoOrder> Order, IQueryable<T> query, string collectionName = null)
        {
            var parameterExpression = Expression.Parameter(typeof(T), "t");

            MethodCallExpression orderByCallExpression = null;

            if (Order != null)
                foreach (var order in Order)
                {
                    if (!string.IsNullOrEmpty(collectionName) &&
                        !order.Column.StartsWith($"{collectionName}.") &&
                        order.Column.Contains("."))
                        continue;

                    var columns = order.Column.Split('.').ToList();
                    if (columns.First() == collectionName) columns.RemoveAt(0);

                    Expression property = parameterExpression;
                    foreach (var member in columns)
                        property = Expression.PropertyOrField(property, member);

                    LambdaExpression lambda = Expression.Lambda(property, parameterExpression);

                    string methodName;

                    if (orderByCallExpression == null)
                    {
                        if (order.Descending)
                            methodName = "OrderByDescending";
                        else
                            methodName = "OrderBy";
                    }
                    else
                    {
                        if (order.Descending)
                            methodName = "ThenByDescending";
                        else
                            methodName = "ThenBy";
                    }

                    orderByCallExpression = Expression.Call(
                        typeof(Queryable),
                        methodName,
                        new Type[] { query.ElementType, property.GetMemberType() },
                        orderByCallExpression ?? query.Expression,
                        Expression.Quote(lambda));
                }

            if (orderByCallExpression != null)
                query = query.Provider.CreateQuery<T>(orderByCallExpression);

            return query;
        }

    }
}
