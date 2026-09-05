using Monjo;

namespace Utilities.Attributes
{
    /// <summary>
    /// Names the Mongo collection of an entity. Preserved for source compatibility; now derives
    /// from <see cref="Monjo.MonjoTableAttribute"/> so every Monjo provider understands it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class MonjoCollectionNameAttribute(string collectionName) : MonjoTableAttribute(collectionName)
    {
        public string CollectionName { get; } = collectionName;
    }
}
