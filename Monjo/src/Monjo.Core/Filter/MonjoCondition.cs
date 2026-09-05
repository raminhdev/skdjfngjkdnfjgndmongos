namespace Utilities.MongoDatabase.Filter
{
    /// <summary>Comparison operators supported by <see cref="MonjoCondition"/>.</summary>
    public enum ComparisonMethods
    {
        Equal,
        NotEqual,
        LessThanOrEqual,
        GreaterThanOrEqual,
        GreaterThan,
        LessThan,
        Contains,
        NotContains,
        IsNull,
        IsNotNull,
        IsEmpty,
        IsNotEmpty
    }

    /// <summary>
    /// A single filter condition. <see cref="Column"/> is a property or column name; a
    /// <c>TypeName.Property</c> value is accepted and the type prefix is ignored.
    /// The structure is deliberately flat and lightweight: no expression trees, no
    /// per-request object graphs.
    /// </summary>
    public class MonjoCondition
    {
        public string Column { get; set; }
        public ComparisonMethods Comparison { get; set; }
        public object Operand { get; set; }
    }
}
