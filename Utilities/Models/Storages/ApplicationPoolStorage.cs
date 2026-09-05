using static Utilities.Constants.RegisterMode;

namespace Utilities.Models.Storages
{
    public class ApplicationPoolStorage : Dictionary<string, ApplicationPool>, ISelfSingletonDependency
    {
    }

    public class ApplicationPool
    {
        public string PreSharedKey { get; set; }
        public string MasterSignature { get; set; }
    }
}
