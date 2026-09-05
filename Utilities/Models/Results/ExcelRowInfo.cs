using NPOI.SS.UserModel;

namespace Utilities.Models.Results
{
    public class ExcelRowInfo
    {
        public int RowNumber { get; set; }
        public IRow Row { get; set; }
    }
}
