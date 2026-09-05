namespace Utilities.Utilities;

public static class PaginationHelper
{
    public static ManualPaginationResult<Type> ApplyManualPagination<Type>(List<Type> data, int? index = 1, int? size = 10)
    {
        var totalCount = data.Count;
        var pageCount = (int)Math.Ceiling(totalCount / (double)size);

        return new ManualPaginationResult<Type>
        {
            PageCount = pageCount,
            TotalCount = totalCount,
            Data = [.. data.Skip((index.Value - 1) * size.Value).Take(size.Value)]
        };
    }
}

// ManualPaginationResult<Type> moved to Monjo.Core (namespace Utilities.Utilities preserved).
