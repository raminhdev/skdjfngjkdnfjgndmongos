using Monjo;
using Monjo.PostgreSQL;
using Npgsql;
using Xunit;

namespace Monjo.Tests
{
    /// <summary>
    /// PostgreSQL runs the same semantic suite against a real server. Requires the environment
    /// variable MONJO_PG_CONNECTION_STRING (host:port credentials to a PG server); without it the
    /// suite skips. Each test creates and drops its own database (isolated).
    /// </summary>
    public class PostgreSqlProviderTests : MonjoProviderSuite
    {
        private static readonly string ServerConnectionString =
            Environment.GetEnvironmentVariable("MONJO_PG_CONNECTION_STRING");

        private readonly string _databaseName = "monjo_tests_" + Guid.NewGuid().ToString("N");
        private readonly string _testConnectionString;
        private MonjoPostgreSqlProvider _provider;

        static PostgreSqlProviderTests()
        {
            if (string.IsNullOrWhiteSpace(ServerConnectionString))
                throw new Xunit.Sdk.SkipException("MONJO_PG_CONNECTION_STRING is not set.");
        }

        public PostgreSqlProviderTests()
        {
            var builder = new NpgsqlConnectionStringBuilder(ServerConnectionString)
            {
                Database = "postgres",
            };

            using var admin = new NpgsqlConnection(builder.ConnectionString);
            admin.Open();
            using var command = admin.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
            command.ExecuteNonQuery();

            _testConnectionString = new NpgsqlConnectionStringBuilder(builder.ConnectionString)
            {
                Database = _databaseName,
            }.ConnectionString;
        }

        protected override string ProviderName => "PostgreSQL";

        protected override Task<IMonjoProvider> CreateAsync()
        {
            _provider = new MonjoPostgreSqlProvider(new MonjoOptions
            {
                Provider = "PostgreSQL",
                ConnectionString = _testConnectionString,
                DatabaseName = _databaseName,
            });
            return Task.FromResult<IMonjoProvider>(_provider);
        }

        public override async Task DisposeAsync()
        {
            await base.DisposeAsync();
            try
            {
                // Clear the pooled connections to the test database first: with live pooled
                // connections PostgreSQL refuses the drop ("database is being accessed by
                // other users") and the connections would leak.
                NpgsqlConnection.ClearAllPools();

                using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(ServerConnectionString)
                {
                    Database = "postgres",
                }.ConnectionString);
                admin.Open();
                using var command = admin.CreateCommand();
                command.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\"";
                command.ExecuteNonQuery();
            }
            catch { /* best effort cleanup */ }
        }

        protected override async Task<long> CountPhysicalAsync()
        {
            await using var connection = new NpgsqlConnection(_testConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM \"People\"";
            var result = await command.ExecuteScalarAsync();
            return result is null ? 0 : Convert.ToInt64(result);
        }

        protected override async Task AssertIndexesCreatedAsync()
        {
            await using var connection = new NpgsqlConnection(_testConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT indexname FROM pg_indexes WHERE tablename = 'People'";
            var names = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                names.Add(reader.GetString(0));

            Assert.Contains("ix_People_Name", names);
            Assert.Contains("ix_People_Age_State", names);
        }
    }
}
