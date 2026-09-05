using Utilities.MongoDatabase.Filter;

namespace Utilities.Models.Updates
{
    /// <summary>Date-range filter descriptor. Namespace preserved for source compatibility.</summary>
    public class DateFieldFilter
    {
        public ComparisonMethods Comparison { get; set; }
        public DateTime? Operand { get; set; } = null;
    }
}
