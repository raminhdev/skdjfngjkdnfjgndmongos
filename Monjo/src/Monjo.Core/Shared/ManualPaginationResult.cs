namespace Utilities.Utilities
{
    /// <summary>Result of client-side (manual) pagination. Namespace preserved for source compatibility.</summary>
    public class ManualPaginationResult<Type>
    {
        public int PageCount { get; set; }
        public int TotalCount { get; set; }
        public List<Type> Data { get; set; }
    }
}
