using M1Mentor.Domain.Collections;

namespace M1Mentor.Services._User.DTOs.Updates
{
    public class UserEditUpdate
    {
        public string PublicKey { get; set; }
        public UserState State { get; set; }
        public string FullName { get; set; }
        public string NickName { get; set; }
        public string UserName { get; set; }
        public UserRole Role { get; set; }
        public string RoleDescription { get; set; }
        public List<string> Permissions { get; set; }
        public string PhoneNumber { get; set; }
        public string EmailAddress { get; set; }
        public string CreatorPublicKey { get; set; }
        public string CreatorName { get; set; }
    }
}