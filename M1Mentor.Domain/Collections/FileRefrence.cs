using Utilities.Attributes;
using Utilities.MongoDatabase.Documents;

namespace M1Mentor.Domain.Collections
{
    [MonjoCollectionName("FileReferences")]
    public class FileReference : BaseDocument
    {
        public string ReferenceId { get; set; } = Guid.NewGuid().ToString("N");
        public string FileMetaId { get; set; }
        public string EntityId { get; set; }
        public EntityType Type { get; set; }
    }

    public enum EntityType { Type1, Type2, Type3 }
}
