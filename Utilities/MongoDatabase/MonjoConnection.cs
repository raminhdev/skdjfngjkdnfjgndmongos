using System.Security.Authentication;
using MongoDB.Driver;
using MongoDB.Driver.Core.Servers;
using Utilities.MongoDatabase.Contracts;
using static Utilities.Constants.RegisterMode;


namespace Utilities.MongoDatabase
{
    public class MonjoConnection : IMonjoConnection, ISingletonDependency
    {
        public IMongoClient Client { get; }
        public IMongoDatabase Database { get; }

        public MonjoConnection(IMonjoSettings settings)
        {
            var mongoUrl = MongoUrl.Create(settings.ConnectionString);
            var clientSettings = MongoClientSettings.FromUrl(mongoUrl);

            // -----------------------------------------------------------------
            // Fix for: System.Security.Authentication.AuthenticationException
            //          ---> Interop+AppleCrypto+SslException: internal error
            //
            // Root cause (confirmed): the driver's default "streaming" heartbeat
            // protocol opens a long-lived TLS connection to EVERY node of the
            // replica set (all 3 shard servers here) at the same time, outside
            // of the normal connection pool (MaxConnecting does NOT apply to
            // these monitor connections). Establishing several concurrent TLS
            // sessions like this triggers a bug in macOS's SecureTransport
            // (AppleCrypto) backend used by .NET's SslStream.
            //
            // Fix: switch heartbeat monitoring from "streaming" to "polling".
            // Polling opens a short-lived connection, does one heartbeat, and
            // closes it — instead of holding 3 concurrent long-lived TLS
            // sessions open at once. This avoids the concurrency pattern that
            // trips the AppleCrypto bug. This only matters for local macOS
            // dev machines; harmless everywhere else (Linux/Windows/prod).
            // -----------------------------------------------------------------
            clientSettings.ServerMonitoringMode = ServerMonitoringMode.Poll;

            clientSettings.SslSettings ??= new SslSettings();
            clientSettings.SslSettings.EnabledSslProtocols = SslProtocols.Tls12;

            clientSettings.MaxConnecting = 1;

            clientSettings.ConnectTimeout = TimeSpan.FromSeconds(30);
            clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(30);
            clientSettings.HeartbeatInterval = TimeSpan.FromSeconds(20);

            Client = new MongoClient(clientSettings);
            Database = Client.GetDatabase(settings.DatabaseName);
        }
    }
}