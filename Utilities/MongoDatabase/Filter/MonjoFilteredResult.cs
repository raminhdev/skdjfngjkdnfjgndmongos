namespace Utilities.MongoDatabase.Filter
{
    public class MonjoFilteredResult<T>
    {
        public int PageCount { get; set; }
        public long TotalCount { get; set; }
        public IList<T> Data { get; set; }
    }
}
