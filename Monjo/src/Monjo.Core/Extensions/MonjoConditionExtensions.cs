using System.ComponentModel;
using System.Linq.Expressions;
using Utilities.MongoDatabase.Filter;

namespace Utilities.MongoDatabase.Extensions
{
    /// <summary>
    /// Translates <see cref="MonjoCondition"/> groups onto an <see cref="IQueryable{T}"/> as a
    /// composed LINQ predicate. Provider-agnostic (pure LINQ); the MongoDB provider is the
    /// consumer today. Each predicate is compiled once by the queryable provider's translator.
    /// </summary>
    public static class MonjoConditionExtensions
    {
        public static IQueryable<T> Apply<T>(
             this IList<IList<MonjoCondition>> Where,
             IQueryable<T> query,
             string collectionName = null)
        {
            var parameterExpression = Expression.Parameter(typeof(T), "t");

            MethodCallExpression whereCallExpression = null;

            if (Where != null)
            {
                Expression andAlso = null;

                foreach (var conditions in Where)
                {
                    Expression orElse = null;

                    foreach (var condition in conditions)
                    {
                        if (!string.IsNullOrEmpty(collectionName) &&
                            !condition.Column.StartsWith($"{collectionName}.") &&
                            condition.Column.Contains("."))
                            continue;

                        var columns = condition.Column.Split('.').ToList();
                        if (columns.First() == collectionName)
                            columns.RemoveAt(0);

                        Expression left = parameterExpression;
                        foreach (var member in columns)
                            left = Expression.PropertyOrField(left, member);

                        var memberType = left.Type;
                        var nonNullableType = Nullable.GetUnderlyingType(memberType) ?? memberType;

                        object rightValue = null;

                        if (nonNullableType.IsEnum)
                            rightValue = Enum.Parse(nonNullableType, condition.Operand?.ToString(), true);
                        else
                            rightValue = Convert.ChangeType(condition.Operand?.ToString(), nonNullableType);

                        Expression right = Expression.Constant(rightValue, memberType);

                        Expression predicateBody;

                        if (condition.Comparison == ComparisonMethods.Equal)
                            predicateBody = Expression.Equal(left, right);
                        else if (condition.Comparison == ComparisonMethods.NotEqual)
                            predicateBody = Expression.NotEqual(left, right);
                        else if (condition.Comparison == ComparisonMethods.GreaterThan)
                            predicateBody = Expression.GreaterThan(left, right);
                        else if (condition.Comparison == ComparisonMethods.GreaterThanOrEqual)
                            predicateBody = Expression.GreaterThanOrEqual(left, right);
                        else if (condition.Comparison == ComparisonMethods.LessThan)
                            predicateBody = Expression.LessThan(left, right);
                        else if (condition.Comparison == ComparisonMethods.LessThanOrEqual)
                            predicateBody = Expression.LessThanOrEqual(left, right);
                        else if (condition.Comparison == ComparisonMethods.Contains)
                        {
                            MethodInfo method = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                            predicateBody = Expression.Call(left, method, right);
                        }
                        else if (condition.Comparison == ComparisonMethods.NotContains)
                        {
                            MethodInfo method = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                            predicateBody = Expression.Not(Expression.Call(left, method, right));
                        }
                        else if (condition.Comparison == ComparisonMethods.IsNull)
                        {
                            predicateBody = Expression.Equal(
                                left,
                                Expression.Constant(null, memberType));
                        }
                        else if (condition.Comparison == ComparisonMethods.IsNotNull)
                        {
                            predicateBody = Expression.NotEqual(
                                left,
                                Expression.Constant(null, memberType));
                        }
                        else if (condition.Comparison == ComparisonMethods.IsEmpty)
                        {
                            right = Expression.Constant(string.Empty, typeof(string));
                            predicateBody = Expression.Equal(left, right);
                        }
                        else if (condition.Comparison == ComparisonMethods.IsNotEmpty)
                        {
                            right = Expression.Constant(string.Empty, typeof(string));
                            predicateBody = Expression.NotEqual(left, right);
                        }
                        else
                            throw new InvalidEnumArgumentException("Unsupported comparison method");

                        if (orElse == null)
                            orElse = predicateBody;
                        else
                            orElse = Expression.OrElse(orElse, predicateBody);
                    }

                    if (andAlso == null)
                        andAlso = orElse;
                    else
                        andAlso = Expression.AndAlso(andAlso, orElse);
                }

                if (andAlso != null)
                    whereCallExpression = Expression.Call(
                        typeof(Queryable),
                        "Where",
                        [query.ElementType],
                        query.Expression,
                        Expression.Lambda<Func<T, bool>>(andAlso, parameterExpression));
            }

            if (whereCallExpression != null)
                query = query.Provider.CreateQuery<T>(whereCallExpression);

            return query;
        }
    }
}
