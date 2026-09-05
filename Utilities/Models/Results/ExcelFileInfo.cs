using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Utilities.Models.Results
{
    public class ExcelFileInfo
    {
        public IWorkbook WorkBook { get; set; }
        public ISheet Sheet { get; set; }
        public Dictionary<CellStyles, XSSFCellStyle> Styles { get; set; }
    }

    public enum CellStyles
    {
        SubjectHeader,
        BorderLessSubjectHeader,
        TitlesHeader,
        BorderLessTitlesHeader,
        PaleBlueHeader,
        Common,
        BorderLess,
        RedFont,
        GreenFont,
        RedBackground,
        PaleRedBackground,
        PaleGreenBackground,
        LightYellowBackground,
        SimpleSplitter1,
        BoldSplitter1,
        SimpleSplitter2,
        BoldSplitter2
    }
}
