namespace Utilities.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class IgnoreLoggingAttribute : Attribute
    {
    }
}
