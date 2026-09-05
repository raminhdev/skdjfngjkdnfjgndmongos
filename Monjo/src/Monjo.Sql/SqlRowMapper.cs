using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;

namespace Monjo.Sql
{
    /// <summary>
    /// Compiled, per-entity row mapping. The reader and writers are built ONCE per type with
    /// expression compilation; per-row work is a single delegate call plus provider data access.
    /// No reflection, no Dapper, no EF — a small hand-rolled mapper with the same cost model.
    /// </summary>
    public sealed class SqlRowMapper<T> where T : class
    {
        public Func<DbDataReader, T> Read { get; }
        public Action<DbCommand, T> ConfigureInsert { get; }
        public Action<DbCommand, T> ConfigureUpdate { get; }
        public Action<DbCommand, T> ConfigureUpsertUpdate { get; }

        private SqlRowMapper(
            Func<DbDataReader, T> read,
            Action<DbCommand, T> insert,
            Action<DbCommand, T> update,
            Action<DbCommand, T> upsertUpdate)
        {
            Read = read;
            ConfigureInsert = insert;
            ConfigureUpdate = update;
            ConfigureUpsertUpdate = upsertUpdate;
        }

        internal static SqlRowMapper<T> Create(SqlEntityMetadata meta, SqlDialect dialect)
            => new(
                BuildReader(meta, dialect),
                BuildWriter(meta, dialect, includeId: true, prefix: ""),
                BuildWriter(meta, dialect, includeId: false, prefix: ""),
                BuildWriter(meta, dialect, includeId: false, prefix: "Up_"));

        private static Func<DbDataReader, T> BuildReader(SqlEntityMetadata meta, SqlDialect dialect)
        {
            var readerParam = Expression.Parameter(typeof(DbDataReader), "reader");
            var target = Expression.Variable(typeof(T), "entity");
            var statements = new List<Expression>(meta.Columns.Count + 1)
            {
                Expression.Assign(target, Expression.New(typeof(T)))
            };

            foreach (var column in meta.Columns)
            {
                var propType = column.Core.PropertyType;
                var ord = Expression.Constant(column.Ordinal);
                var isNull = Expression.Call(readerParam,
                    typeof(DbDataReader).GetMethod(nameof(DbDataReader.IsDBNull), [typeof(int)])!, ord);

                var readCall = Expression.Convert(
                    Expression.Call(
                        typeof(SqlValueConverters),
                        nameof(SqlValueConverters.Read),
                        null,
                        readerParam,
                        ord,
                        Expression.Constant(column.Core.NonNullableType),
                        Expression.Constant(dialect.SupportsNativeGuid),
                        Expression.Constant(dialect.ReadsDateTimeAsText)),
                    propType);

                Expression read;
                if (propType.IsValueType && Nullable.GetUnderlyingType(propType) is null)
                    read = Expression.Condition(isNull, Expression.Default(propType), readCall);
                else if (propType.IsValueType)
                    read = Expression.Condition(
                        isNull,
                        Expression.Convert(Expression.Default(Nullable.GetUnderlyingType(propType)!), propType),
                        readCall);
                else
                    read = Expression.Condition(isNull, Expression.Constant(null, propType), readCall);

                statements.Add(Expression.Assign(Expression.Property(target, column.Core.Property), read));
            }

            statements.Add(target);
            var lambda = Expression.Lambda<Func<DbDataReader, T>>(Expression.Block(statements), readerParam);
            return lambda.Compile();
        }

        private static Action<DbCommand, T> BuildWriter(SqlEntityMetadata meta, SqlDialect dialect, bool includeId, string prefix)
        {
            var createParameter = typeof(DbCommand).GetMethod(nameof(DbCommand.CreateParameter))!;
            var addParameter = typeof(DbParameterCollection)
                .GetMethod(nameof(DbParameterCollection.Add), [typeof(DbParameter)])!;
            var toDb = typeof(SqlValueConverters).GetMethod(nameof(SqlValueConverters.ToDb))!;
            var toDbValue = dialect.GetType().GetMethod(nameof(SqlDialect.ToDbValue))!;

            var cmdParam = Expression.Parameter(typeof(DbCommand), "cmd");
            var entityParam = Expression.Parameter(typeof(T), "entity");

            var statements = new List<Expression>(meta.Columns.Count * 3);
            foreach (var column in meta.Columns.Where(c => includeId || !c.Core.IsId))
            {
                var param = Expression.Call(cmdParam, createParameter);
                statements.Add(Expression.Assign(
                    Expression.Property(param, nameof(DbParameter.ParameterName)),
                    Expression.Constant(column.ParamName(prefix))));
                statements.Add(Expression.Assign(
                    Expression.Property(param, nameof(DbParameter.Value)),
                    Expression.Call(Expression.Constant(dialect), toDbValue,
                        Expression.Call(toDb, Expression.Property(entityParam, column.Core.Property)))));
                statements.Add(Expression.Call(
                    Expression.Property(cmdParam, nameof(DbCommand.Parameters)),
                    addParameter, param));
            }

            var lambda = Expression.Lambda<Action<DbCommand, T>>(Expression.Block(statements), cmdParam, entityParam);
            return lambda.Compile();
        }
    }
}
