using Utilities.MongoDatabase.Filter;

namespace Monjo.Sql
{
    /// <summary>One SQL parameter (name + value).</summary>
    public readonly record struct SqlParameter(string Name, object? Value);

    /// <summary>
    /// A translated MonjoQuery: WHERE fragment + ORDER BY fragment + parameters + page bounds.
    /// Value type where it counts; the string fragments are the only per-request allocations
    /// (small, and necessary to compose the final SQL text).
    /// </summary>
    public sealed class SqlQueryPlan
    {
        public SqlQueryPlan(string whereSql, string orderSql, List<SqlParameter>? parameters, int? limit, int offset)
        {
            WhereSql = whereSql;
            OrderSql = orderSql;
            Parameters = parameters ?? Array.Empty<SqlParameter>();
            Limit = limit;
            Offset = offset;
        }

        public string WhereSql { get; }
        public string OrderSql { get; }
        public IReadOnlyList<SqlParameter> Parameters { get; }
        public int? Limit { get; }
        public int Offset { get; }

        public static readonly SqlQueryPlan Empty = new(string.Empty, string.Empty, null, null, 0);
    }

    /// <summary>
    /// MonjoQuery → parameterized SQL. Direct translation: no expression trees, no LINQ provider,
    /// no compiled query objects per request. Column references are resolved against the cached
    /// entity metadata; operands are converted once per condition.
    /// </summary>
    public static class SqlQueryTranslator
    {
        /// <summary>Translates the full query (where + order + page).</summary>
        public static SqlQueryPlan Translate(MonjoQuery? query, SqlEntityMetadata meta)
        {
            if (query is null)
                return SqlQueryPlan.Empty;

            var parameters = new List<SqlParameter>(8);
            var whereSql = TranslateWhere(query.Where, meta, parameters);
            var orderSql = TranslateOrder(query.Order, meta);

            int? limit = null;
            var offset = 0;
            var page = query.Page;
            if (page is { Size: > 0 })
            {
                limit = page.Size;
                offset = Math.Max(0, page.Index - 1) * page.Size;
            }

            return new SqlQueryPlan(whereSql, orderSql, parameters, limit, offset);
        }

        /// <summary>Translates only the WHERE clause (used by count/exists/update/delete).</summary>
        public static string TranslateWhere(IList<IList<MonjoCondition>>? where, SqlEntityMetadata meta, List<SqlParameter> parameters)
        {
            if (where is null || where.Count == 0)
                return string.Empty;

            var andParts = new List<string>(where.Count);
            foreach (var group in where)
            {
                if (group is null || group.Count == 0)
                    continue;

                var orParts = new List<string>(group.Count);
                foreach (var condition in group)
                {
                    var column = meta.FindColumn(condition.Column)
                        ?? throw new MonjoException(
                            $"Unknown column or property '{condition.Column}' for table '{meta.Core.TableName}'. " +
                            "Condition columns must reference entity property names or column names.");

                    orParts.Add(TranslateCondition(condition, column, parameters));
                }

                if (orParts.Count > 0)
                    andParts.Add(orParts.Count == 1 ? orParts[0] : "(" + string.Join(" OR ", orParts) + ")");
            }

            return andParts.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", andParts);
        }

        private static string TranslateCondition(MonjoCondition condition, SqlColumnMetadata column, List<SqlParameter> parameters)
        {
            var col = column.Quoted;

            switch (condition.Comparison)
            {
                case ComparisonMethods.Equal:
                    return col + " = @" + AddParameter(condition, column, parameters);
                case ComparisonMethods.NotEqual:
                    return col + " <> @" + AddParameter(condition, column, parameters);
                case ComparisonMethods.GreaterThan:
                    return col + " > @" + AddParameter(condition, column, parameters);
                case ComparisonMethods.GreaterThanOrEqual:
                    return col + " >= @" + AddParameter(condition, column, parameters);
                case ComparisonMethods.LessThan:
                    return col + " < @" + AddParameter(condition, column, parameters);
                case ComparisonMethods.LessThanOrEqual:
                    return col + " <= @" + AddParameter(condition, column, parameters);
                case ComparisonMethods.Contains:
                    return col + " LIKE @" + AddLikeParameter(condition.Operand, parameters) + " ESCAPE '\\'";
                case ComparisonMethods.NotContains:
                    return col + " NOT LIKE @" + AddLikeParameter(condition.Operand, parameters) + " ESCAPE '\\'";
                case ComparisonMethods.IsNull:
                    return col + " IS NULL";
                case ComparisonMethods.IsNotNull:
                    return col + " IS NOT NULL";
                case ComparisonMethods.IsEmpty:
                    return col + " = ''";
                case ComparisonMethods.IsNotEmpty:
                    return col + " <> ''";
                default:
                    throw new MonjoNotSupportedException($"Comparison '{condition.Comparison}' is not supported by the SQL providers.");
            }
        }

        private static string AddParameter(MonjoCondition condition, SqlColumnMetadata column, List<SqlParameter> parameters)
        {
            var name = "p" + parameters.Count;
            var value = SqlValueConverters.ConvertOperand(condition.Operand, column.Core.NonNullableType);
            parameters.Add(new SqlParameter(name, value));
            return name;
        }

        private static string AddLikeParameter(object? operand, List<SqlParameter> parameters)
        {
            var name = "p" + parameters.Count;
            var value = operand is null ? null : "%" + SqlValueConverters.EscapeLike(operand.ToString()!) + "%";
            parameters.Add(new SqlParameter(name, value));
            return name;
        }

        private static string TranslateOrder(IList<MonjoOrder>? order, SqlEntityMetadata meta)
        {
            if (order is null || order.Count == 0)
                return string.Empty;

            var parts = new List<string>(order.Count);
            foreach (var o in order)
            {
                var column = meta.FindColumn(o.Column)
                    ?? throw new MonjoException(
                        $"Unknown column or property '{o.Column}' for table '{meta.Core.TableName}'.");
                parts.Add(column.Quoted + (o.Descending ? " DESC" : " ASC"));
            }

            return " ORDER BY " + string.Join(", ", parts);
        }
    }
}
