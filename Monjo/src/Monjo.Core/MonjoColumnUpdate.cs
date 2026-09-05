namespace Monjo
{
    /// <summary>
    /// Provider-independent partial update: a set of column values. Keys are property names or
    /// column names (resolved against the entity metadata at execution time).
    /// The object is lightweight and immutable once built; it is converted into a native
    /// <c>UPDATE</c> (SQL) or <c>UpdateDefinition</c> (Mongo) per call.
    /// </summary>
    public sealed class MonjoColumnUpdate
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Sets the value of a column (property name or physical column name).</summary>
        public MonjoColumnUpdate Set(string propertyOrColumn, object? value)
        {
            if (string.IsNullOrEmpty(propertyOrColumn))
                throw new ArgumentException("Column name is required.", nameof(propertyOrColumn));
            _values[propertyOrColumn] = value;
            return this;
        }

        public int Count => _values.Count;

        internal IReadOnlyDictionary<string, object?> Values => _values;

        internal bool IsEmpty => _values.Count == 0;

        internal bool Contains(string propertyOrColumn) => _values.ContainsKey(propertyOrColumn);
    }
}
