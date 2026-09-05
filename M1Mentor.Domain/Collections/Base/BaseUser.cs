using MongoDB.Bson.Serialization.Attributes;
using Utilities.MongoDatabase.Documents;

namespace M1Mentor.Domain.Collections.Base;

public class BaseUser : BaseDocument
{
    public string PublicKey { get; set; } = Guid.NewGuid().ToString("N");

    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    public List<string> Permissions { get; set; } = [];

    public string PasswordHash { get; set; }

    public List<DateTime> LoginDates { get; set; } = [];

    [BsonDefaultValue(UserState.Active)] public UserState State { get; set; }
}