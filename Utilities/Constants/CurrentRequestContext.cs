namespace Utilities.Constants
{
    public static class CurrentRequestContext
    {
        private static readonly AsyncLocal<RequestUserInfo> _current = new();

        public static RequestUserInfo User
        {
            get => _current.Value;
            set => _current.Value = value;
        }


        public static string PublicKey => User?.PublicKey;
        public static string UserFullName => User?.UserFullName;
        public static string Role => User?.Role;
        public static string DisplayInfo => User?.DisplayInfo;
    }
}
