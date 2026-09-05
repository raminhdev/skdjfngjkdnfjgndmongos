using MongoDB.Bson.Serialization.Attributes;

namespace Utilities.MongoDatabase.Documents
{
    /// <summary>
    /// The original Mongo base document, preserved untouched (same properties, same BSON
    /// attributes, same storage format). Audit fields are filled from <see cref="Monjo.MonjoActorContext"/>,
    /// which applications bridge to their request context at startup — identical values to the
    /// previous <c>CurrentRequestContext</c> behaviour.
    /// </summary>
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
            var actor = Monjo.MonjoActorContext.Current;
            CreatedBy = actor.PublicKey ?? "system";
            CreatedByInfo = actor.DisplayInfo ?? "system : system";
        }
    }

}
