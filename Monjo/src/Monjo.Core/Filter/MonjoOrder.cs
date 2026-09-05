namespace Utilities.MongoDatabase.Filter
{
    /// <summary>Ordering by a single column. <see cref="Column"/> is a property or column name.</summary>
    public class MonjoOrder
    {
        public string Column { get; set; }
        public bool Descending { get; set; }
    }
}
