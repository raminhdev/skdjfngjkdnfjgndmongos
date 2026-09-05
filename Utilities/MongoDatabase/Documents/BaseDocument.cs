using MongoDB.Bson.Serialization.Attributes;
using Utilities.Constants;

namespace Utilities.MongoDatabase.Documents
{
    public class BaseDocument
    {
        [BsonId]
        [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonDefaultValue(null)] public string CreatedBy { get; set; } = null;
        [BsonDefaultValue(null)] public string CreatedByInfo { get; set; } = null;
        public DateTime CreatedMoment { get; set; } = DateTime.UtcNow;

        [BsonDefaultValue(null)] public string ModifiedBy { get; set; } = null;
        [BsonDefaultValue(null)] public string ModifiedByInfo { get; set; } = null;
        public DateTime? ModifiedMoment { get; set; } = null;

        [BsonDefaultValue(null)] public string DeletedBy { get; set; } = null;
        [BsonDefaultValue(null)] public string DeletedByInfo { get; set; } = null;
        [BsonDefaultValue(false)] public bool IsDeleted { get; set; }
        public DateTime? DeletedMoment { get; set; } = null;

        public BaseDocument()
        {
            var user = CurrentRequestContext.User ?? new RequestUserInfo();

            CreatedBy = user.PublicKey;
            CreatedByInfo = user.DisplayInfo;
        }
    }

}
