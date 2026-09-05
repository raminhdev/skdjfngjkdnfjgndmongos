using M1Mentor.Services._Common.DTOs.Results;

namespace M1Mentor.Services._FileMeta.DTOs.Results
{
    public class FileMetaResult : CommonResult
    {
        public string FileMetaId { get; set; }
        public string StoredName { get; set; }     // generated unique name
        public string OriginalName { get; set; }   // uploaded file name
        public long Size { get; set; }
        public string Extension { get; set; }
        public int ReferenceCount { get; set; } = 0;
    }
}
