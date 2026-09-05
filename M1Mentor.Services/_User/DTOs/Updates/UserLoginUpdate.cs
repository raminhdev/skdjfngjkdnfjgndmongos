namespace M1Mentor.Services._User.DTOs.Updates
{
    public class UserLoginUpdate
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string CaptchaCode { get; set; }
    }
}
