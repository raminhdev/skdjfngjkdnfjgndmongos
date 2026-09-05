using MongoDB.Driver;

namespace Utilities.MongoDatabase.Contracts
{
    /// <summary>
    /// The original Mongo connection contract (native client + database handles), preserved for
    /// source compatibility. Provided by <see cref="Monjo.MongoDB.MongoMonjoConnection"/> — the
    /// same singleton the new <c>Monjo.IMonjoConnection</c> resolves to.
    /// </summary>
    public interface IMonjoConnection
    {
        IMongoClient Client { get; }
        IMongoDatabase Database { get; }
    }
}
