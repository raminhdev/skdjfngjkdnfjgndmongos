using Utilities.MongoDatabase.Filter;

namespace Utilities.Models.Updates
{
    public class DateFieldFilter
    {
        public ComparisonMethods Comparison { get; set; }
        public DateTime? Operand { get; set; } = null;
    }
}
