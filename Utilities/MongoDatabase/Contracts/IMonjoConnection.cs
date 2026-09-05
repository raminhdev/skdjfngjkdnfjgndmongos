using MongoDB.Driver;

namespace Utilities.MongoDatabase.Contracts
{
    public interface IMonjoConnection
    {
        IMongoClient Client { get; }
        IMongoDatabase Database { get; }
    }
}
