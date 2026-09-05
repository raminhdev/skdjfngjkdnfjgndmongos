namespace Utilities.Constants
{
    public class RequestUserInfo
    {
        public string PublicKey { get; set; } = "system";
        public string UserFullName { get; set; } = "system";
        public string Role { get; set; } = "system";
        public string Type { get; set; } = "system";
        public List<string> Permissions { get; set; } = [];

        public string DisplayInfo => $"{Role} : {UserFullName}";
    }
}
