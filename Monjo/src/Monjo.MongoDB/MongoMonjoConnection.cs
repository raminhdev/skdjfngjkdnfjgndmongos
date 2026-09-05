using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Authentication;
using MongoDB.Driver;
using MongoDB.Driver.Core.Servers;

namespace Monjo.MongoDB
{
    /// <summary>
    /// Monjo's MongoDB connection: a single reused <see cref="IMongoClient"/> (thread-safe by
    /// driver design) with cached <see cref="IMongoCollection{TEntity}"/> handles.
    /// Also implements the legacy <c>Utilities.MongoDatabase.Contracts.IMonjoConnection</c>
    /// so pre-existing application code keeps working unchanged.
    /// </summary>
    public sealed class MongoMonjoConnection :
        Monjo.IMonjoConnection,
        Utilities.MongoDatabase.Contracts.IMonjoConnection
    {
        /// <summary>The native driver client. Reused for the process lifetime.</summary>
        public IMongoClient Client { get; }

        /// <summary>The native driver database handle. Reused for the process lifetime.</summary>
        public IMongoDatabase Database { get; }

        public string ProviderName => "MongoDB";

        public string DatabaseName => Database.DatabaseNamespace.DatabaseName;

        private readonly MonjoOptions _options;
        private readonly ConcurrentDictionary<Type, IMongoCollection> _collections = new();

        /// <summary>
        /// Per-type readiness state (gate key + work delegate), built once per type per
        /// connection. After the first call for a type, every repository operation's readiness
        /// check costs two dictionary lookups and an IsCompleted check — no allocations.
        /// </summary>
        private readonly ConcurrentDictionary<Type, (string Key, Func<CancellationToken, Task> Work)> _readiness = new();

        public MongoMonjoConnection(MonjoOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            _options = options;
            MongoBsonDefaults.Register();

            var mongoUrl = MongoUrl.Create(options.ConnectionString
                ?? throw new MonjoException("Monjo MongoDB provider: ConnectionString is required."));

            var clientSettings = MongoClientSettings.FromUrl(mongoUrl);

            // Preserved behaviour of the original MonjoConnection:
            // - Polling server monitoring (short-lived heartbeat connections; avoids the
            //   macOS SecureTransport/AppleCrypto concurrency bug, harmless elsewhere).
            // - TLS 1.2 enforcement.
            clientSettings.ServerMonitoringMode =
                options.Mongo.ServerMonitoringMode.Equals("Stream", StringComparison.OrdinalIgnoreCase)
                    ? ServerMonitoringMode.Stream
                    : ServerMonitoringMode.Poll;

            if (options.Mongo.EnforceTls12)
            {
                clientSettings.SslSettings ??= new SslSettings();
                clientSettings.SslSettings.EnabledSslProtocols = SslProtocols.Tls12;
            }

            clientSettings.MaxConnecting = options.Mongo.MaxConnecting;
            clientSettings.ConnectTimeout = TimeSpan.FromSeconds(options.Mongo.ConnectTimeoutSeconds);
            clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(options.Mongo.ServerSelectionTimeoutSeconds);
            clientSettings.HeartbeatInterval = TimeSpan.FromSeconds(options.Mongo.HeartbeatIntervalSeconds);

            // No database call happens here: the client connects lazily on first operation.
            Client = new MongoClient(clientSettings);
            Database = Client.GetDatabase(options.DatabaseName
                ?? throw new MonjoException("Monjo MongoDB provider: DatabaseName is required."));
        }

        /// <summary>Collection name resolution: [MonjoTable]/[MonjoCollectionName] attribute, else the type name.</summary>
        internal static string GetTableName(Type type)
            => type.GetCustomAttribute<MonjoTableAttribute>()?.Name ?? type.Name;

        /// <summary>Gets (or creates once) the cached collection handle for <typeparamref name="T"/>.</summary>
        public IMongoCollection<T> GetCollection<T>() where T : class
            => (IMongoCollection<T>)_collections.GetOrAdd(typeof(T), _ => Database.GetCollection<T>(GetTableName(typeof(T))));

        public IMonjoRepository<T> CreateRepository<T>() where T : class
            => new MongoMonjoRepository<T>(this, GetCollection<T>());

        public Task EnsureEntityReadyAsync<T>(CancellationToken cancellationToken = default) where T : class
        {
            if (!_options.AutoCreateIndexes)
                return Task.CompletedTask;

            if (_readiness.TryGetValue(typeof(T), out var readiness))
                return EntityReadinessGate.EnsureAsync(readiness.Key, readiness.Work, cancellationToken);

            var tableName = GetTableName(typeof(T));
            // The key includes the database: index creation belongs to a specific database.
            var key = "MongoDB:" + Database.DatabaseNamespace.DatabaseName + ":" + tableName;
            var database = Database;
            var work = new Func<CancellationToken, Task>(token => MongoIndexManager.EnsureIndexesCoreAsync<T>(database, token));
            _readiness[typeof(T)] = (key, work);
            return EntityReadinessGate.EnsureAsync(key, work, cancellationToken);
        }

        public async Task<MonjoTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            var session = Client.StartSession();
            try
            {
                await session.StartTransactionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationFailureException or MongoInsufficientServerSupportException)
            {
                session.Dispose();
                throw new MonjoNotSupportedException(
                    "MongoDB transactions require a replica set deployment. The current server is standalone, " +
                    "so no transaction can be started. Run the database as a replica set (or a managed equivalent) to enable transactions.",
                    e);
            }

            return new MonjoTransaction(
                native: new MongoTransactionBridge(session),
                commit: token => session.CommitTransactionAsync(cancellationToken: token),
                rollback: token => session.AbortTransactionAsync(cancellationToken: token),
                disposeNative: () =>
                {
                    session.Dispose();
                    return ValueTask.CompletedTask;
                });
        }
    }

    /// <summary>Monjo's MongoDB provider.</summary>
    public sealed class MongoMonjoProvider : Monjo.IMonjoProvider
    {
        public string Name => "MongoDB";
        public MonjoOptions Options { get; }
        public MongoMonjoConnection Connection { get; }

        public MongoMonjoProvider(MonjoOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Connection = new MongoMonjoConnection(options);
        }

        public Task EnsureEntityReadyAsync<T>(CancellationToken cancellationToken = default) where T : class
            => Connection.EnsureEntityReadyAsync<T>(cancellationToken);

        public Task<MonjoTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Connection.BeginTransactionAsync(cancellationToken);
    }
}
