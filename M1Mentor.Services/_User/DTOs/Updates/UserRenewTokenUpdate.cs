using Utilities.Attributes;

namespace M1Mentor.Services._User.DTOs.Updates
{
    public class UserRenewTokenUpdate
    {
        public string access_token { get; set; }


        [StringInputValidation(ErrorMessage = "refresh_token is required.")]
        public string refresh_token { get; set; }


        [StringInputValidation(ErrorMessage = "grant_type must be 'refresh_token'.")]
        public string grant_type { get; set; }

        public string client_id { get; set; }
        public string client_secret { get; set; }
    }
}