using Utilities.Attributes;
using Utilities.MongoDatabase.Documents;

namespace M1Mentor.Domain.Collections
{
    [MonjoCollectionName("FileMetas")]
    public class FileMeta : BaseDocument
    {
        public string FileMetaId { get; set; } = Guid.NewGuid().ToString("N");
        public string Hash { get; set; }
        public string StoredName { get; set; }     // generated unique name
        public string OriginalName { get; set; }   // uploaded file name
        public string StoragePath { get; set; }    // provider-relative key / object key (never an absolute path leak)
        public string StorageProvider { get; set; } // "Local" | "GridFs" | "S3" | "MinIO"
        public string BucketName { get; set; }      // object-store bucket (S3/MinIO); null for Local/GridFs
        public string UploadedBy { get; set; }      // PublicKey of the uploading user
        public string MimeType { get; set; }

        public long Size { get; set; }
        public string Extension { get; set; }

        public int ReferenceCount { get; set; } = 0;
        public DateTime? MarkedForDeletionAt { get; set; } = null;
    }
}
