namespace Utilities.MongoDatabase.Documents
{
    public static class BaseDocumentUtilities
    {
        public static IQueryable<T> FilterByDate<T>(IQueryable<T> query, DateTime? from = null, DateTime? to = null)
            where T : BaseDocument
        {
            if (from != null && to != null)
                query = query.Where(q => q.CreatedMoment >= from.Value && q.CreatedMoment <= to.Value);
            else if (from == null && to != null)
                query = query.Where(q => q.CreatedMoment <= to.Value);
            else if (from != null && to == null)
                query = query.Where(q => q.CreatedMoment >= from.Value);

            return query;
        }
    }
}
