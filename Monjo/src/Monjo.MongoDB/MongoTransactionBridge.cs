using MongoDB.Driver;

namespace Monjo.MongoDB
{
    /// <summary>Provider-native bridge for a Mongo transaction: the client session handle.</summary>
    public sealed class MongoTransactionBridge
    {
        public MongoTransactionBridge(IClientSessionHandle session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public IClientSessionHandle Session { get; }
    }
}
