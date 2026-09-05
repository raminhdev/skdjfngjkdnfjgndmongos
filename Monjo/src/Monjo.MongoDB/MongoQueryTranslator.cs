using System.Globalization;
using System.Linq.Expressions;
using System.Collections.Concurrent;
using MongoDB.Driver;
using Monjo.Metadata;
using Utilities.MongoDatabase.Filter;

namespace Monjo.MongoDB
{
    /// <summary>
    /// Translates <see cref="MonjoQuery"/> onto native MongoDB filter/sort definitions —
    /// server-side execution, no LINQ provider, no BsonDocument materialization.
    /// Column selectors are cached per (type, column); per-request allocations are limited to
    /// the small predicate/filter tree the driver needs.
    /// </summary>
    public static class MongoQueryTranslator
    {
        /// <summary>Builds the WHERE filter from MonjoQuery conditions (AND of OR groups).</summary>
        public static FilterDefinition<T> BuildWhere<T>(IList<IList<MonjoCondition>>? where) where T : class
        {
            if (where is null || where.Count == 0)
                return Builders<T>.Filter.Empty;

            var parameter = Expression.Parameter(typeof(T), "t");
            Expression? combined = null;

            foreach (var group in where)
            {
                if (group is null || group.Count == 0)
                    continue;

                Expression? orElse = null;
                foreach (var condition in group)
                {
                    var body = BuildConditionBody<T>(condition, parameter);
                    orElse = orElse is null ? body : Expression.OrElse(orElse, body);
                }

                if (orElse is null)
                    continue;

                combined = combined is null ? orElse : Expression.AndAlso(combined, orElse);
            }

            if (combined is null)
                return Builders<T>.Filter.Empty;

            return Builders<T>.Filter.Where(Expression.Lambda<Func<T, bool>>(combined, parameter));
        }

        private static Expression BuildConditionBody<T>(MonjoCondition condition, ParameterExpression parameter) where T : class
        {
            var meta = MonjoEntityMetadata.Get<T>();
            var column = meta.FindColumn(condition.Column)
                ?? throw new MonjoException(
                    $"Unknown column or property '{condition.Column}' for '{typeof(T).Name}'. " +
                    "Condition columns must reference entity property names.");

            var property = Expression.Property(parameter, column.Property);
            var nonNullableType = column.NonNullableType;
            var operand = MonjoOperandConversion.ConvertOperand(condition.Operand, nonNullableType);

            return condition.Comparison switch
            {
                ComparisonMethods.Equal => Expression.Equal(property, ConstantFor(column, operand)),
                ComparisonMethods.NotEqual => Expression.NotEqual(property, ConstantFor(column, operand)),
                ComparisonMethods.GreaterThan => Expression.GreaterThan(property, ConstantFor(column, operand)),
                ComparisonMethods.GreaterThanOrEqual => Expression.GreaterThanOrEqual(property, ConstantFor(column, operand)),
                ComparisonMethods.LessThan => Expression.LessThan(property, ConstantFor(column, operand)),
                ComparisonMethods.LessThanOrEqual => Expression.LessThanOrEqual(property, ConstantFor(column, operand)),
                ComparisonMethods.Contains => StringMethodCall(property, operand, negate: false),
                ComparisonMethods.NotContains => StringMethodCall(property, operand, negate: true),
                ComparisonMethods.IsNull => Expression.Equal(property, Expression.Constant(null, column.PropertyType)),
                ComparisonMethods.IsNotNull => Expression.NotEqual(property, Expression.Constant(null, column.PropertyType)),
                ComparisonMethods.IsEmpty => Expression.Equal(property, Expression.Constant(string.Empty)),
                ComparisonMethods.IsNotEmpty => Expression.NotEqual(property, Expression.Constant(string.Empty)),
                _ => throw new MonjoNotSupportedException($"Comparison '{condition.Comparison}' is not supported.")
            };
        }

        /// <summary>Constant typed to the (possibly nullable) property type so predicate types line up.</summary>
        private static Expression ConstantFor(MonjoColumnMetadata column, object? value)
            => Expression.Constant(value, column.PropertyType);

        private static Expression StringMethodCall(Expression property, object? operand, bool negate)
        {
            var stringType = property.Type == typeof(string)
                ? property.Type
                : Nullable.GetUnderlyingType(property.Type) == typeof(string)
                    ? typeof(string)
                    : throw new MonjoNotSupportedException("Contains/NotContains is only supported on string columns.");

            if (property.Type != stringType)
                property = Expression.Convert(property, stringType);

            var contains = typeof(string).GetMethod("Contains", [typeof(string)])!;
            var call = Expression.Call(property, contains,
                Expression.Constant(operand is null ? string.Empty : operand.ToString()));
            return negate ? Expression.Not(call) : call;
        }

        /// <summary>Builds the ORDER BY sort definition from MonjoQuery orders.</summary>
        public static SortDefinition<T>? BuildSort<T>(IList<MonjoOrder>? order) where T : class
        {
            if (order is null || order.Count == 0)
                return null;

            SortDefinition<T>? sort = null;
            foreach (var o in order)
            {
                var selector = GetSelector<T>(o.Column);
                var definition = o.Descending
                    ? Builders<T>.Sort.Descending(selector)
                    : Builders<T>.Sort.Ascending(selector);
                sort = sort is null ? definition : sort.And(definition);
            }

            return sort;
        }

        /// <summary>Builds an equality filter on the identifier (no soft-delete combination).</summary>
        public static FilterDefinition<T> BuildIdFilter<T>(object id) where T : class
        {
            var meta = MonjoEntityMetadata.Get<T>();
            var column = meta.Id
                ?? throw new MonjoException($"Entity '{typeof(T).Name}' has no identifier property.");

            var value = ConvertId(id, column.NonNullableType);

            var parameter = Expression.Parameter(typeof(T), "t");
            var body = Expression.Equal(
                Expression.Property(parameter, column.Property),
                Expression.Constant(value, column.NonNullableType));
            return Builders<T>.Filter.Where(Expression.Lambda<Func<T, bool>>(body, parameter));
        }

        private static object ConvertId(object id, Type target)
        {
            if (target.IsInstanceOfType(id))
                return id;
            if (target == typeof(Guid))
                return id is Guid g ? g : Guid.Parse(id.ToString()!);
            if (target == typeof(string))
                return id.ToString()!;
            return Convert.ChangeType(id, target, CultureInfo.InvariantCulture);
        }

        /// <summary>The soft-delete predicate (IsDeleted == false) or null when the entity has none.</summary>
        public static FilterDefinition<T>? GetSoftDeleteFilter<T>() where T : class
        {
            var meta = MonjoEntityMetadata.Get<T>();
            if (meta.IsDeleted is null)
                return null;

            var parameter = Expression.Parameter(typeof(T), "t");
            var body = Expression.Equal(
                Expression.Property(parameter, meta.IsDeleted.Property),
                Expression.Constant(false));
            return Builders<T>.Filter.Where(Expression.Lambda<Func<T, bool>>(body, parameter));
        }

        /// <summary>Combines a filter with the soft-delete predicate when the entity supports it.</summary>
        public static FilterDefinition<T> CombineWithSoftDelete<T>(FilterDefinition<T> filter) where T : class
        {
            var soft = GetSoftDeleteFilter<T>();
            return soft is null ? filter : soft & filter;
        }

        // ------------------------------------------------------------------ selector cache

        private sealed class PerTypeSelectorCache
        {
            public readonly ConcurrentDictionary<string, Expression> Selectors = new();
        }

        private static readonly ConcurrentDictionary<Type, PerTypeSelectorCache> _selectorCaches = new();

        /// <summary>Gets (or builds once) the object-typed member selector for a column reference.</summary>
        public static Expression<Func<T, object>> GetSelector<T>(string columnReference) where T : class
        {
            var meta = MonjoEntityMetadata.Get<T>();
            var column = meta.FindColumn(columnReference)
                ?? throw new MonjoException(
                    $"Unknown column or property '{columnReference}' for '{typeof(T).Name}'.");

            var cache = _selectorCaches.GetOrAdd(typeof(T), _ => new PerTypeSelectorCache());
            if (cache.Selectors.TryGetValue(column.ColumnName, out var cached))
                return (Expression<Func<T, object>>)cached;

            var parameter = Expression.Parameter(typeof(T), "t");
            var selector = Expression.Lambda<Func<T, object>>(
                Expression.Convert(Expression.Property(parameter, column.Property), typeof(object)),
                parameter);

            cache.Selectors.TryAdd(column.ColumnName, selector);
            return selector;
        }
    }
}
