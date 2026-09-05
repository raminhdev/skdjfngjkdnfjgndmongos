using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Utilities.Models.Results;

namespace Utilities.Services.Contracts
{
    public interface IExportService
    {
        void CreateCell(ISheet sheet, IRow currentRow, int cellIndex, string value, XSSFCellStyle style, int columnWidth = 5800);

        void MergedRegion(ISheet sheet, int firstRow, int lastRow, int firstCol, int lastCol);

        ExcelRowInfo GetLastRowToContinueInformation(ISheet sheet, float rowHeight = 27);

        ExcelFileInfo CreateWorkbookWithBasicHeader(string sheetName, List<string> titleHeaderTitles = null, double titlesHeaderFontSize = 11,
            int titlesHeaderRowHeight = 27, string subjectHeaderTitle = null, double subjectHeaderFontSize = 16, int subjectHeaderRowHeight = 31,
            int headersColumnWidth = 5800);
    }
}