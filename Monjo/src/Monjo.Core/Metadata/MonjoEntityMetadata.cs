using System.Collections.Concurrent;
using System.Reflection;
using Monjo;

namespace Monjo.Metadata
{
    /// <summary>Metadata for one persisted column. Value type; stored once per entity type.</summary>
    public readonly struct MonjoColumnMetadata
    {
        public MonjoColumnMetadata(PropertyInfo property, string columnName, bool isId)
        {
            Property = property;
            ColumnName = columnName;
            IsId = isId;
            NonNullableType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        }

        public PropertyInfo Property { get; }
        public string ColumnName { get; }
        public bool IsId { get; }
        public Type NonNullableType { get; }
        public Type PropertyType => Property.PropertyType;
        public bool IsEnum => NonNullableType.IsEnum;
    }

    /// <summary>
    /// Immutable, per-entity-type persistence metadata (table name, identifier, columns, audit
    /// fields, indexes). Built lazily ONCE per type on first repository use and cached for the
    /// process lifetime — request hot paths perform zero reflection.
    /// </summary>
    public sealed class MonjoEntityMetadata
    {
        public Type EntityType { get; }
        public string TableName { get; }
        public MonjoColumnMetadata? Id { get; }
        public IReadOnlyList<MonjoColumnMetadata> Columns { get; }
        public IReadOnlyList<MonjoIndexDefinition> Indexes { get; }

        // Audit fields by convention (only present when the entity model declares them).
        public MonjoColumnMetadata? CreatedMoment { get; }
        public MonjoColumnMetadata? ModifiedMoment { get; }
        public MonjoColumnMetadata? DeletedMoment { get; }
        public MonjoColumnMetadata? CreatedBy { get; }
        public MonjoColumnMetadata? CreatedByInfo { get; }
        public MonjoColumnMetadata? ModifiedBy { get; }
        public MonjoColumnMetadata? ModifiedByInfo { get; }
        public MonjoColumnMetadata? DeletedBy { get; }
        public MonjoColumnMetadata? DeletedByInfo { get; }
        public MonjoColumnMetadata? IsDeleted { get; }

        public bool HasSoftDelete => IsDeleted is not null;
        public bool HasAudit => CreatedBy is not null || ModifiedBy is not null;

        private readonly Dictionary<string, MonjoColumnMetadata> _byPropertyName;
        private readonly Dictionary<string, MonjoColumnMetadata> _byColumnName;

        internal MonjoEntityMetadata(
            Type entityType,
            string tableName,
            MonjoColumnMetadata? id,
            List<MonjoColumnMetadata> columns,
            List<MonjoIndexDefinition> indexes,
            MonjoColumnMetadata? createdMoment,
            MonjoColumnMetadata? modifiedMoment,
            MonjoColumnMetadata? deletedMoment,
            MonjoColumnMetadata? createdBy,
            MonjoColumnMetadata? createdByInfo,
            MonjoColumnMetadata? modifiedBy,
            MonjoColumnMetadata? modifiedByInfo,
            MonjoColumnMetadata? deletedBy,
            MonjoColumnMetadata? deletedByInfo,
            MonjoColumnMetadata? isDeleted)
        {
            EntityType = entityType;
            TableName = tableName;
            Id = id;
            Columns = columns;
            Indexes = indexes;
            CreatedMoment = createdMoment;
            ModifiedMoment = modifiedMoment;
            DeletedMoment = deletedMoment;
            CreatedBy = createdBy;
            CreatedByInfo = createdByInfo;
            ModifiedBy = modifiedBy;
            ModifiedByInfo = modifiedByInfo;
            DeletedBy = deletedBy;
            DeletedByInfo = deletedByInfo;
            IsDeleted = isDeleted;

            _byPropertyName = new Dictionary<string, MonjoColumnMetadata>(StringComparer.Ordinal);
            _byColumnName = new Dictionary<string, MonjoColumnMetadata>(StringComparer.Ordinal);
            var ciByProperty = new Dictionary<string, MonjoColumnMetadata>(StringComparer.OrdinalIgnoreCase);
            var ciByColumn = new Dictionary<string, MonjoColumnMetadata>(StringComparer.OrdinalIgnoreCase);

            foreach (var column in columns)
            {
                _byPropertyName.TryAdd(column.Property.Name, column);
                _byColumnName.TryAdd(column.ColumnName, column);
                ciByProperty.TryAdd(column.Property.Name, column);
                ciByColumn.TryAdd(column.ColumnName, column);
            }
            _ciByPropertyName = ciByProperty;
            _ciByColumnName = ciByColumn;
        }

        private Dictionary<string, MonjoColumnMetadata> _ciByPropertyName;
        private Dictionary<string, MonjoColumnMetadata> _ciByColumnName;

        /// <summary>
        /// Resolves a condition/order column reference to a physical column. Accepts a property
        /// name, a column name, or a <c>Type.Prefix</c>-dotted name (last segment is resolved).
        /// Returns null when unresolvable.
        /// </summary>
        public string? ResolveColumn(string reference)
        {
            if (string.IsNullOrEmpty(reference))
                return null;

            var name = reference;
            var dot = name.LastIndexOf('.');
            if (dot >= 0)
                name = name[(dot + 1)..];

            if (_byPropertyName.TryGetValue(name, out var byProp)
                || _ciByPropertyName.TryGetValue(name, out byProp))
                return byProp.ColumnName;

            if (_byColumnName.TryGetValue(name, out var byCol)
                || _ciByColumnName.TryGetValue(name, out byCol))
                return byCol.ColumnName;

            return null;
        }

        public MonjoColumnMetadata? FindColumn(string reference)
        {
            var name = reference;
            var dot = name.LastIndexOf('.');
            if (dot >= 0)
                name = name[(dot + 1)..];

            if (_byPropertyName.TryGetValue(name, out var byProp)
                || _ciByPropertyName.TryGetValue(name, out byProp))
                return byProp;

            if (_byColumnName.TryGetValue(name, out var byCol)
                || _ciByColumnName.TryGetValue(name, out byCol))
                return byCol;

            return null;
        }

        /// <summary>Builds metadata for <typeparamref name="T"/> (cached; reflection happens only here).</summary>
        public static MonjoEntityMetadata Get<T>() where T : class
            => Get(typeof(T));

        public static MonjoEntityMetadata Get(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            return MonjoMetadataCache._cache.GetOrAdd(type, Build);
        }

        private static MonjoEntityMetadata Build(Type type)
        {
            var tableName = type.GetCustomAttribute<MonjoTableAttribute>()?.Name ?? type.Name;

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0
                            && p.CanRead
                            && p.GetCustomAttribute<MonjoIgnoreAttribute>() is null)
                .ToList();

            MonjoColumnMetadata? Find(string name) => properties
                .FirstOrDefault(p => p.Name == name) is { } p ? ToColumn(p, false) : null;

            var idProperty = properties.FirstOrDefault(p => p.GetCustomAttribute<MonjoIdAttribute>() is not null)
                             ?? properties.FirstOrDefault(p => p.Name == "Id"
                                && (p.PropertyType == typeof(string) || p.PropertyType == typeof(Guid)
                                    || p.PropertyType == typeof(int) || p.PropertyType == typeof(long)));

            var columns = new List<MonjoColumnMetadata>(properties.Count);
            foreach (var p in properties)
                columns.Add(ToColumn(p, ReferenceEquals(p, idProperty)));

            var indexes = type.GetCustomAttributes<MonjoIndexAttribute>()
                .Select(a => MonjoIndexDefinition.FromAttribute(a, tableName))
                .ToList();

            return new MonjoEntityMetadata(
                type,
                tableName,
                idProperty is null ? null : columns.First(c => c.IsId),
                columns,
                indexes,
                Find("CreatedMoment"),
                Find("ModifiedMoment"),
                Find("DeletedMoment"),
                Find("CreatedBy"),
                Find("CreatedByInfo"),
                Find("ModifiedBy"),
                Find("ModifiedByInfo"),
                Find("DeletedBy"),
                Find("DeletedByInfo"),
                Find("IsDeleted"));
        }

        private static MonjoColumnMetadata ToColumn(PropertyInfo property, bool isId)
        {
            var columnName = property.GetCustomAttribute<MonjoColumnAttribute>()?.Name ?? property.Name;
            return new MonjoColumnMetadata(property, columnName, isId);
        }
    }

    /// <summary>Process-wide metadata cache (lock-free; reflection only on the first use of a type).</summary>
    public static class MonjoMetadataCache
    {
        internal static readonly ConcurrentDictionary<Type, MonjoEntityMetadata> _cache = new();
    }
}
