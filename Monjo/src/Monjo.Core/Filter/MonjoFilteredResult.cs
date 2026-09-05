namespace Utilities.MongoDatabase.Filter
{
    /// <summary>
    /// Provider-independent paged result. Both the MongoDB and the SQL providers produce this
    /// type directly — no intermediate result object is converted or re-wrapped.
    /// Kept as a mutable class (settable members) for source compatibility with the original API.
    /// </summary>
    public class MonjoFilteredResult<T>
    {
        public int PageCount { get; set; }
        public long TotalCount { get; set; }
        public IList<T> Data { get; set; }
    }
}
