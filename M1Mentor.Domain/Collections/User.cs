using M1Mentor.Domain.Collections.Base;
using MongoDB.Bson.Serialization.Attributes;
using Utilities.Attributes;

namespace M1Mentor.Domain.Collections
{
    [MonjoCollectionName("Users")]
    public class User : BaseUser
    {
        [BsonDefaultValue(UserRole.Creator)] public UserRole Role { get; set; }
        [BsonDefaultValue(null)] public string RoleDescription { get; set; }
        public string UserName { get; set; }
        public string FullName { get; set; }
        public string NickName { get; set; }
        public string PhoneNumber { get; set; }
        public string EmailAddress { get; set; }
        public string RefreshTokenHash { get; set; }
        public DateTime? RefreshTokenExpiresAt { get; set; }
        
    }

    public enum UserState { Active, Ban, Archived }
    public enum UserRole { Admin, User, Creator}
}
