namespace Utilities.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class MonjoCollectionNameAttribute(string collectionName) : Attribute
    {
        public string CollectionName { get; } = collectionName;
    }
}