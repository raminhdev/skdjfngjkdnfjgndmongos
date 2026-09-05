namespace Utilities.Attributes
{
    public class SecurityAttribute(bool encodeInputs = true, bool ignoreLinks = false, bool disable = false) : Attribute
    {
        public bool EncodeInputs { get; set; } = encodeInputs;
        public bool IgnoreLinks { get; set; } = ignoreLinks;
        public bool Disable { get; set; } = disable;
    }
}
