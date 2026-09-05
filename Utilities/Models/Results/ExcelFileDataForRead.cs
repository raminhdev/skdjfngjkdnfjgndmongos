using NPOI.SS.UserModel;

namespace Utilities.Models.Results
{
    public record ExcelFileDataForRead
    {
        public IWorkbook WorkBook { get; set; }
        public ISheet Sheet { get; set; }
        public Dictionary<string, int> FieldsWithTheirIndex { get; set; }
    }
}
