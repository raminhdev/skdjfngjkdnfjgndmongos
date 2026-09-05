using MongoDB.Bson;
using MongoDB.Driver;
using Monjo;
using Monjo.MongoDB;
using Xunit;

namespace Monjo.Tests
{
    /// <summary>
    /// MongoDB runs the same semantic suite against a real server. Requires the environment
    /// variable MONGO_CONNECTION_STRING (e.g. mongodb://localhost:27017); without it the suite
    /// skips. Each test uses its own database (isolated) which is dropped afterwards.
    /// </summary>
    public class MongoProviderTests : MonjoProviderSuite
    {
        private static readonly string ConnectionString =
            Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");

        private readonly string _databaseName = "monjo_tests_" + Guid.NewGuid().ToString("N");
        private MongoMonjoProvider _provider;

        static MongoProviderTests()
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
                throw new Xunit.Sdk.SkipException("MONGO_CONNECTION_STRING is not set.");
        }

        protected override string ProviderName => "MongoDB";

        protected override Task<IMonjoProvider> CreateAsync()
        {
            _provider = new MongoMonjoProvider(new MonjoOptions
            {
                Provider = "MongoDB",
                ConnectionString = ConnectionString,
                DatabaseName = _databaseName,
            });
            return Task.FromResult<IMonjoProvider>(_provider);
        }

        public override async Task DisposeAsync()
        {
            await base.DisposeAsync();
            if (_provider is not null)
            {
                try { await _provider.Connection.Client.DropDatabaseAsync(_databaseName); }
                catch { /* best effort cleanup */ }
            }
        }

        protected override async Task<long> CountPhysicalAsync()
        {
            var collection = _provider.Connection.GetCollection<TestPerson>();
            return await collection.CountDocumentsAsync(BsonDocument.Parse("{}"));
        }

        protected override async Task AssertIndexesCreatedAsync()
        {
            var collection = _provider.Connection.GetCollection<TestPerson>();
            var names = new List<string>();
            await foreach (var index in collection.Indexes.ListAsync())
                names.Add(index.Name);

            Assert.Contains("ix_People_Name", names);
            Assert.Contains("ix_People_Age_State", names);
        }
    }
}
