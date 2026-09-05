namespace Monjo
{
    /// <summary>
    /// Names the physical table/collection of an entity. When absent, the CLR type name is used.
    /// The legacy Mongo attribute <c>[MonjoCollectionName]</c> derives from this type, so both are
    /// understood by every provider.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class MonjoTableAttribute(string name) : Attribute
    {
        public string Name { get; } = name;
    }

    /// <summary>Marks the identifier property (defaults to a property named <c>Id</c>).</summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class MonjoIdAttribute : Attribute
    {
    }

    /// <summary>Overrides the physical column name for a property.</summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class MonjoColumnAttribute(string name) : Attribute
    {
        public string Name { get; } = name;
    }

    /// <summary>Excludes a property from persistence mapping.</summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class MonjoIgnoreAttribute : Attribute
    {
    }

    /// <summary>
    /// Declares an index on the entity. Created idempotently once per process by the provider
    /// (at startup if <c>EnsureEntityReadyAsync</c> is called, otherwise lazily on first use).
    /// Column names are property names (or physical column names).
    /// </summary>
    /// <example><code>[MonjoIndex("PhoneNumber", unique: true)]</code> or
    /// <code>[MonjoIndex("A,B", descending: new[] { false, true })]</code></example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class MonjoIndexAttribute(string columns) : Attribute
    {
        /// <summary>Comma-separated column list, e.g. "PhoneNumber" or "A,B".</summary>
        public string Columns { get; } = columns;

        /// <summary>Per-column descending flags (optional; shorter than the column list = all ASC).</summary>
        public bool[] Descending { get; set; }

        public bool Unique { get; set; }

        /// <summary>Explicit index name; defaults to <c>ix_&lt;Table&gt;_&lt;columns&gt;</c>.</summary>
        public string Name { get; set; }
    }
}
