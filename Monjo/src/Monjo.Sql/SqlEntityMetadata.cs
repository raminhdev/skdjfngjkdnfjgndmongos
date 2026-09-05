using System.Globalization;
using Monjo.Metadata;

namespace Monjo.Sql
{
    /// <summary>One SQL column: mapping + SELECT ordinal + SQL type.</summary>
    public sealed class SqlColumnMetadata
    {
        public SqlColumnMetadata(MonjoColumnMetadata core, int ordinal, string sqlType)
        {
            Core = core;
            Ordinal = ordinal;
            SqlType = sqlType;
        }

        public MonjoColumnMetadata Core { get; }
        public int Ordinal { get; }
        public string SqlType { get; }

        public string Quoted => SqlDialect.Quote(ColumnName);
        public string ColumnName => Core.ColumnName;

        /// <summary>
        /// Parameter name for this column. The identifier is always <c>@Id</c> (regardless of
        /// column renames) so the cached templates and the repository's explicit <c>@Id</c>
        /// binding stay in sync.
        /// </summary>
        public string ParamName(string prefix = "")
            => Core.IsId ? "@Id" : "@" + prefix + Core.ColumnName;
    }

    /// <summary>
    /// The per-entity SQL plan: all SQL text is built ONCE (per type per provider) and cached on
    /// the provider. Per-request work is limited to appending the (parameterized)
    /// WHERE / ORDER BY / LIMIT / OFFSET clauses produced by <see cref="SqlQueryTranslator"/>.
    /// </summary>
    public sealed class SqlEntityMetadata
    {
        public MonjoEntityMetadata Core { get; }
        public string TableQuoted { get; }
        public IReadOnlyList<SqlColumnMetadata> Columns { get; }
        public SqlColumnMetadata IdColumn { get; }

        /// <summary>Soft-delete predicate ("" when the entity has no IsDeleted column).</summary>
        public string SoftDeleteFilterSql { get; }

        public string SelectColumnsSql { get; }
        public string GetByIdSql { get; }
        public string CountSql { get; }
        public string ExistsSql { get; }
        public string InsertSql { get; }
        public string UpdateSql { get; }
        public string UpsertSql { get; }
        public string HardDeleteByIdSql { get; }
        public string SoftDeleteByIdSql { get; }
        public string CreateSchemaSql { get; }

        /// <summary>Converts a caller-supplied identifier (string/Guid/numeric) to the id column's CLR type.</summary>
        public Func<object, object?> ConvertId { get; }

        public bool HasSoftDelete => Core.HasSoftDelete;

        private readonly Dictionary<string, SqlColumnMetadata> _byReference;

        internal SqlEntityMetadata(
            MonjoEntityMetadata core,
            string tableQuoted,
            List<SqlColumnMetadata> columns,
            SqlColumnMetadata idColumn,
            string softDeleteFilterSql,
            string selectColumnsSql,
            string getByIdSql,
            string countSql,
            string existsSql,
            string insertSql,
            string updateSql,
            string upsertSql,
            string hardDeleteByIdSql,
            string softDeleteByIdSql,
            string createSchemaSql,
            Func<object, object?> convertId)
        {
            Core = core;
            TableQuoted = tableQuoted;
            Columns = columns;
            IdColumn = idColumn;
            SoftDeleteFilterSql = softDeleteFilterSql;
            SelectColumnsSql = selectColumnsSql;
            GetByIdSql = getByIdSql;
            CountSql = countSql;
            ExistsSql = existsSql;
            InsertSql = insertSql;
            UpdateSql = updateSql;
            UpsertSql = upsertSql;
            HardDeleteByIdSql = hardDeleteByIdSql;
            SoftDeleteByIdSql = softDeleteByIdSql;
            CreateSchemaSql = createSchemaSql;
            ConvertId = convertId;

            _byReference = new Dictionary<string, SqlColumnMetadata>(Columns.Count * 3, StringComparer.OrdinalIgnoreCase);
            foreach (var c in columns)
            {
                _byReference.TryAdd(c.Core.Property.Name, c);
                _byReference.TryAdd(c.ColumnName, c);
            }
        }

        /// <summary>Resolves a property name / column name / "Type.Prop" reference to a SQL column.</summary>
        public SqlColumnMetadata? FindColumn(string reference)
        {
            if (string.IsNullOrEmpty(reference))
                return null;

            var dot = reference.LastIndexOf('.');
            var name = dot >= 0 ? reference[(dot + 1)..] : reference;
            return _byReference.TryGetValue(name, out var column) ? column : null;
        }

        /// <summary>
        /// Built from core metadata + dialect and cached per (type, provider) — the only reflection
        /// in this layer is inside the core metadata cache.
        /// </summary>
        internal static SqlEntityMetadata Build(SqlMonjoProvider provider, Type entityType)
        {
            var core = MonjoEntityMetadata.Get(entityType);
            var dialect = provider.Dialect;

            var id = core.Id
                ?? throw new MonjoException(
                    $"Entity '{entityType.Name}' has no identifier property for the SQL provider. " +
                    "Add a property named 'Id' (string/Guid/int/long) or mark one with [MonjoId].");

            var table = SqlDialect.Quote(core.TableName);
            var softFilter = core.IsDeleted is { } del
                ? $"{SqlDialect.Quote(del.ColumnName)} = {dialect.FalseLiteral}"
                : string.Empty;

            var columns = new List<SqlColumnMetadata>(core.Columns.Count);
            var notMappable = new List<string>();
            var ordinal = 0;
            foreach (var c in core.Columns)
            {
                string sqlType;
                try
                {
                    sqlType = dialect.GetSqlType(c.NonNullableType);
                }
                catch (MonjoNotSupportedException)
                {
                    notMappable.Add(c.Property.Name);
                    continue;
                }

                columns.Add(new SqlColumnMetadata(c, ordinal++, sqlType));
            }

            if (notMappable.Count > 0)
                throw new MonjoNotSupportedException(
                    $"Entity '{entityType.Name}' has properties not mappable to SQL: {string.Join(", ", notMappable)}. " +
                    "Supported SQL column types: string, bool, int, long, short, byte, double, float, decimal, DateTime, Guid, byte[], " +
                    "enum (stored as text) and their nullable variants. Complex properties (lists, nested objects) are not supported by the SQL providers in v1.");

            if (!columns.Any(c => c.Core.IsId))
                throw new MonjoException($"Entity '{entityType.Name}': the identifier column was not mappable to SQL.");

            var idColumn = columns.First(c => c.Core.IsId);
            var selectColumns = string.Join(", ", columns.Select(c => c.Quoted));

            var getByIdWhere = idColumn.Quoted + " = @Id" + (softFilter.Length > 0 ? " AND " + softFilter : string.Empty);
            var getByIdSql = $"SELECT {selectColumns} FROM {table} WHERE {getByIdWhere}";

            // NOTE: CountSql/ExistsSql are deliberately bare (no WHERE). The soft-delete filter and
            // the user predicate are combined in exactly one place (SqlEntityMetadataExtensions.
            // BuildWhereSql) at execution time — baking the soft filter here would produce
            // "WHERE ... WHERE ..." when a user filter is appended.
            var countSql = $"SELECT COUNT(*) FROM {table}";
            var existsSql = $"SELECT 1 FROM {table}";

            var insertSql = $"INSERT INTO {table} ({selectColumns}) VALUES (" +
                string.Join(", ", columns.Select(c => c.ParamName())) + ")";

            var nonId = columns.Where(c => !c.Core.IsId).ToList();
            var updateSql = $"UPDATE {table} SET " +
                string.Join(", ", nonId.Select(c => $"{c.Quoted} = {c.ParamName()}")) +
                $" WHERE {idColumn.Quoted} = @Id" +
                (softFilter.Length > 0 ? " AND " + softFilter : string.Empty);

            var upsertSql = $"INSERT INTO {table} ({selectColumns}) VALUES (" +
                string.Join(", ", columns.Select(c => c.ParamName())) +
                $") ON CONFLICT ({idColumn.Quoted}) DO UPDATE SET " +
                string.Join(", ", nonId.Select(c => $"{c.Quoted} = {c.ParamName("Up_")}"));

            var hardDeleteByIdSql = $"DELETE FROM {table} WHERE {idColumn.Quoted} = @Id";

            var softDeleteByIdSql = string.Empty;
            if (core.IsDeleted is { } delCol)
            {
                var setClauses = new List<string> { $"{SqlDialect.Quote(delCol.ColumnName)} = {dialect.TrueLiteral}" };
                if (core.DeletedMoment is { } dm) setClauses.Add($"{SqlDialect.Quote(dm.ColumnName)} = @DeletedMoment");
                if (core.DeletedBy is { } db) setClauses.Add($"{SqlDialect.Quote(db.ColumnName)} = @DeletedBy");
                if (core.DeletedByInfo is { } dbi) setClauses.Add($"{SqlDialect.Quote(dbi.ColumnName)} = @DeletedByInfo");
                softDeleteByIdSql = $"UPDATE {table} SET {string.Join(", ", setClauses)} WHERE {idColumn.Quoted} = @Id" +
                    (softFilter.Length > 0 ? " AND " + softFilter : string.Empty);
            }

            var schemaDefs = new List<string>(columns.Count);
            foreach (var c in columns)
            {
                var notNull = c.Core.IsId
                    || (c.Core.PropertyType.IsValueType && Nullable.GetUnderlyingType(c.Core.PropertyType) is null);
                var def = $"{c.Quoted} {c.SqlType}{(notNull ? " NOT NULL" : string.Empty)}";
                if (c.Core.IsId)
                    def += " PRIMARY KEY";
                schemaDefs.Add(def);
            }
            var createSchemaSql = $"CREATE TABLE IF NOT EXISTS {table} ({string.Join(", ", schemaDefs)})";

            var idType = id.NonNullableType;
            Func<object, object?> convertId = idType == typeof(Guid)
                ? static v => v is Guid g ? g : Guid.Parse(v.ToString()!)
                : idType == typeof(int)
                    ? static v => v is int i ? i : Convert.ToInt32(v, CultureInfo.InvariantCulture)
                    : idType == typeof(long)
                        ? static v => v is long l ? l : Convert.ToInt64(v, CultureInfo.InvariantCulture)
                        : static v => v.ToString()!;

            return new SqlEntityMetadata(
                core, table, columns, idColumn, softFilter, selectColumns,
                getByIdSql, countSql, existsSql, insertSql, updateSql, upsertSql,
                hardDeleteByIdSql, softDeleteByIdSql, createSchemaSql, convertId);
        }

        /// <summary>Index DDL for one declared index (idempotent).</summary>
        public string BuildIndexSql(MonjoIndexDefinition index)
        {
            var columnParts = new List<string>(index.Columns.Count);
            foreach (var c in index.Columns)
            {
                var col = FindColumn(c.Property)
                    ?? throw new MonjoException($"Index '{index.Name}' references unknown column '{c.Property}'.");
                columnParts.Add(col.Quoted + (c.Descending ? " DESC" : string.Empty));
            }

            return "CREATE " + (index.Unique ? "UNIQUE " : string.Empty) +
                   "INDEX IF NOT EXISTS " +
                   $"{SqlDialect.Quote(index.Name)} ON {TableQuoted} ({string.Join(", ", columnParts)})";
        }
    }
}
