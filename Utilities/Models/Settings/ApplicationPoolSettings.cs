namespace Utilities.Models.Settings
{
    public class ApplicationPoolSettings
    {
        public Application[] Applications { get; set; }
    }

    public class Application
    {
        public string ApplicationId { get; set; }
        public string PreSharedKey { get; set; }
        public string MasterSignature { get; set; }
    }
}