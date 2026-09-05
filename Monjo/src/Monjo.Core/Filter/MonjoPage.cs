namespace Utilities.MongoDatabase.Filter
{
    /// <summary>
    /// Page-based pagination. 1-based <see cref="Index"/> and <see cref="Size"/> rows per page,
    /// matching the pre-existing behaviour. A non-positive <see cref="Size"/> disables paging.
    /// The architecture keeps the door open for future keyset/cursor pagination (the query
    /// translator consumes page bounds independently of the transport).
    /// </summary>
    public class MonjoPage
    {
        public int Index { get; set; } = 1;
        public int Size { get; set; } = 50;
    }
}
