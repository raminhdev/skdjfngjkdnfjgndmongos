using M1Mentor.Services._Common.DTOs.Results;
using M1Mentor.Domain.Collections;

namespace M1Mentor.Services._User.DTOs.Results
{
    public class UserFilteredResult : CommonResult
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
        public List<DateTime> LoginDates { get; set; }
    }
}
