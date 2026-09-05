using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Utilities.Exceptions.Common;
using Utilities.Models.Results;
using Utilities.Services.Contracts;
using static Utilities.Constants.RegisterMode;

namespace Utilities.Services
{
    public class ExportService : IExportService, ISingletonDependency
    {
        public void CreateCell(ISheet sheet, IRow currentRow, int cellIndex, string value, XSSFCellStyle style, int columnWidth = 5800)
        {
            try
            {
                ICell cell = currentRow.CreateCell(cellIndex);
                cell.SetCellValue(value);
                cell.CellStyle = style;

                sheet.SetColumnWidth(cellIndex, columnWidth);
            }
            catch (Exception ex)
            {
                throw new BaseException(ex.Message);
            }
        }

        public void MergedRegion(ISheet sheet, int firstRow, int lastRow, int firstCol, int lastCol)
        {
            try
            {
                NPOI.SS.Util.CellRangeAddress MergedBatch = new NPOI.SS.Util.CellRangeAddress(firstRow, lastRow, firstCol, lastCol);
                sheet.AddMergedRegion(MergedBatch);
            }
            catch (Exception ex)
            {
                throw new BaseException(ex.Message);
            }
        }

        public ExcelRowInfo GetLastRowToContinueInformation(ISheet sheet, float rowHeight = 27)
        {
            try
            {
                var rowNumber = sheet.LastRowNum + 1;
                var row = sheet.CreateRow(rowNumber);
                row.HeightInPoints = rowHeight;

                return new ExcelRowInfo
                {
                    RowNumber = rowNumber,
                    Row = row
                };
            }
            catch (Exception ex)
            {
                throw new BaseException(ex.Message);
            }
        }

        public ExcelFileInfo CreateWorkbookWithBasicHeader(string sheetName, List<string> titleHeaderTitles = null, double titlesHeaderFontSize = 11,
            int titlesHeaderRowHeight = 27, string subjectHeaderTitle = null, double subjectHeaderFontSize = 16, int subjectHeaderRowHeight = 31, int headersColumnWidth = 5800)
        {
            try
            {
                IWorkbook wb = new XSSFWorkbook();
                ICreationHelper createHelper = wb.GetCreationHelper();
                ISheet sheet = wb.CreateSheet(sheetName);
                var styles = new Dictionary<CellStyles, XSSFCellStyle>();
                var rowNumber = 0;

                #region Specify Cell Styles

                var mainHeaderCellStyle = CreateCellStyle(wb, fontSize: subjectHeaderFontSize, isBold: true, fontName: "SimSun");
                styles[CellStyles.SubjectHeader] = mainHeaderCellStyle;

                var borderLessMainHeaderStyle = CreateCellStyle(wb, fontSize: 16, isBold: true, borderStyle: BorderStyle.None);
                styles[CellStyles.BorderLessSubjectHeader] = borderLessMainHeaderStyle;

                var headerCellStyle = CreateCellStyle(wb, fontSize: titlesHeaderFontSize, isBold: true, wrapText: true);
                styles[CellStyles.TitlesHeader] = headerCellStyle;

                var paleBlueHeader = CreateCellStyle(wb, fontSize: titlesHeaderFontSize, isBold: true, wrapText: true,
                    backgroundColor: new XSSFColor([180, 198, 231], null));
                styles[CellStyles.PaleBlueHeader] = paleBlueHeader;

                var borderLessTitlesHeaderStyle = CreateCellStyle(wb, fontSize: 14, isBold: true, wrapText: true, borderStyle: BorderStyle.None);
                styles[CellStyles.BorderLessTitlesHeader] = borderLessTitlesHeaderStyle;

                var cellStyle = CreateCellStyle(wb, wrapText: true);
                styles[CellStyles.Common] = cellStyle;

                var nonCellStyle = CreateCellStyle(wb, borderStyle: BorderStyle.None);
                styles[CellStyles.BorderLess] = nonCellStyle;

                var redFontCellStyle = CreateCellStyle(wb, fontColor: IndexedColors.Red.Index);
                styles[CellStyles.RedFont] = redFontCellStyle;

                var greenFontCellStyle = CreateCellStyle(wb, fontColor: IndexedColors.Green.Index);
                styles[CellStyles.GreenFont] = greenFontCellStyle;

                var redCellStyle = CreateCellStyle(wb, backgroundColor: new XSSFColor([255, 0, 0], null));
                styles[CellStyles.RedBackground] = redCellStyle;

                var paleRedCellStyle = CreateCellStyle(wb, backgroundColor: new XSSFColor([255, 105, 105], null));
                styles[CellStyles.PaleRedBackground] = paleRedCellStyle;

                var paleGreenCellStyle = CreateCellStyle(wb, backgroundColor: new XSSFColor([198, 224, 180], null));
                styles[CellStyles.PaleGreenBackground] = paleGreenCellStyle;

                var lightYellowCellStyle = CreateCellStyle(wb, backgroundColor: new XSSFColor([255, 230, 153], null));
                styles[CellStyles.LightYellowBackground] = lightYellowCellStyle;

                var simpleSplitter1 = CreateCellStyle(wb, wrapText: true, backgroundColor: new XSSFColor([251, 228, 213], null));
                styles[CellStyles.SimpleSplitter1] = simpleSplitter1;

                var boldSplitter1 = CreateCellStyle(wb, isBold: true, wrapText: true, backgroundColor: new XSSFColor([251, 228, 213], null));
                styles[CellStyles.BoldSplitter1] = boldSplitter1;

                var simpleSplitter2 = CreateCellStyle(wb, wrapText: true, backgroundColor: new XSSFColor([217, 226, 243], null));
                styles[CellStyles.SimpleSplitter2] = simpleSplitter2;

                var boldSplitter2 = CreateCellStyle(wb, isBold: true, wrapText: true, backgroundColor: new XSSFColor([217, 226, 243], null));
                styles[CellStyles.BoldSplitter2] = boldSplitter2;

                #endregion

                #region Create Subject Header

                if (!string.IsNullOrEmpty(subjectHeaderTitle))
                {
                    var titlesCount = titleHeaderTitles.Count;
                    MergedRegion(sheet, 0, 0, 0, titlesCount - 1);

                    var mainHeaderRow = sheet.CreateRow(rowNumber);
                    mainHeaderRow.HeightInPoints = subjectHeaderRowHeight;

                    CreateCell(sheet, mainHeaderRow, 0, subjectHeaderTitle, mainHeaderCellStyle, headersColumnWidth);

                    rowNumber += 1;
                }

                #endregion

                #region Create Titles Header

                if (titleHeaderTitles != null && titleHeaderTitles.Count >= 1)
                {
                    var headerRow = sheet.CreateRow(rowNumber);
                    headerRow.HeightInPoints = titlesHeaderRowHeight;

                    titleHeaderTitles.ToList().ForEach(title =>
                    {
                        CreateCell(sheet, headerRow, titleHeaderTitles.IndexOf(title), title, headerCellStyle, headersColumnWidth);
                    });
                }

                #endregion

                return new ExcelFileInfo
                {
                    WorkBook = wb,
                    Sheet = sheet,
                    Styles = styles
                };
            }
            catch (Exception ex)
            {
                throw new BaseException(ex.Message);
            }
        }

        #region Private Methods

        private XSSFCellStyle CreateCellStyle(IWorkbook workbook, double fontSize = 11, bool isBold = false, string fontName = "Calibri", short fontColor = 0,
            bool wrapText = false, BorderStyle borderStyle = BorderStyle.Medium, XSSFColor backgroundColor = null)
        {
            var font = (XSSFFont)workbook.CreateFont();
            font.FontHeightInPoints = fontSize;
            font.IsBold = isBold;
            font.FontName = fontName;
            font.Color = fontColor;

            var cellStyle = (XSSFCellStyle)workbook.CreateCellStyle();
            cellStyle.SetFont(font);
            cellStyle.VerticalAlignment = VerticalAlignment.Center;
            cellStyle.Alignment = HorizontalAlignment.Center;
            cellStyle.WrapText = wrapText;
            cellStyle.BorderLeft = borderStyle;
            cellStyle.BorderTop = borderStyle;
            cellStyle.BorderRight = borderStyle;
            cellStyle.BorderBottom = borderStyle;

            if (backgroundColor != null)
            {
                cellStyle.SetFillForegroundColor(backgroundColor);
                cellStyle.FillPattern = FillPattern.SolidForeground;
            }

            return cellStyle;
        }

        #endregion
    }
}