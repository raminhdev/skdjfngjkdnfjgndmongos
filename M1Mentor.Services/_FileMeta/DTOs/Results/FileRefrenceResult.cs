using M1Mentor.Domain.Collections;
using M1Mentor.Services._Common.DTOs.Results;

namespace M1Mentor.Services._FileMeta.DTOs.Results
{
    public class FileReferenceResult : CommonResult
    {
        public string ReferenceId { get; set; }
        public string FileMetaId { get; set; }
        public string EntityId { get; set; }
        public EntityType Type { get; set; }
    }
}
