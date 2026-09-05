namespace _Utilities.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class BypassJwtAttribute : Attribute { }
}

namespace _Utilities.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class AllowExpiredTokenAttribute : Attribute { }
}