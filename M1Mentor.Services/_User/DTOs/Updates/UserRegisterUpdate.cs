using System.ComponentModel.DataAnnotations;
using M1Mentor.Domain.Collections;

namespace M1Mentor.Services._User.DTOs.Updates
{
    public class UserRegisterUpdate
    {
        public string FullName { get; set; }
        public string NickName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public UserRole Role { get; set; }
        public string RoleDescription { get; set; }
        public List<string> Permissions { get; set; }
        public string PhoneNumber { get; set; }
        [EmailAddress]
        public string EmailAddress { get; set; }
    }
}