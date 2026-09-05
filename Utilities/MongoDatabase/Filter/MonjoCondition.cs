namespace Utilities.MongoDatabase.Filter
{
    public enum ComparisonMethods { Equal, NotEqual, LessThanOrEqual, GreaterThanOrEqual, GreaterThan, LessThan, Contains, NotContains, IsNull, IsNotNull, IsEmpty, IsNotEmpty }
    public class MonjoCondition
    {
        public string Column { get; set; }
        public ComparisonMethods Comparison { get; set; }
        public object Operand { get; set; }
    }
}
