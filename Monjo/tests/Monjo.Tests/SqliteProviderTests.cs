using Microsoft.Data.Sqlite;
using Monjo;
using Monjo.SQLite;
using Xunit;

namespace Monjo.Tests
{
    /// <summary>
    /// SQLite runs the full suite against a real (temp-file) database — no external service
    /// needed, so the whole semantic suite is verified in CI.
    /// </summary>
    public class SqliteProviderTests : MonjoProviderSuite
    {
        private readonly string _dbPath;

        public SqliteProviderTests()
        {
            var dir = Path.Combine(Path.GetTempPath(), "monjo-tests");
            Directory.CreateDirectory(dir);
            _dbPath = Path.Combine(dir, $"{RunId}.db");
        }

        protected override string ProviderName => "SQLite";

        protected override Task<IMonjoProvider> CreateAsync()
            => Task.FromResult<IMonjoProvider>(new MonjoSqliteProvider(new MonjoOptions
            {
                Provider = "SQLite",
                ConnectionString = _dbPath,
                DatabaseName = "test",
            }));

        public override async Task DisposeAsync()
        {
            await base.DisposeAsync();
            try
            {
                // Close every pooled (idle) SQLite connection in the process first: pooled
                // connections hold the database file open, which would keep the file (and the
                // WAL/SHM side files) around and leak file handles. Only after the pool is
                // empty is the file safe to delete.
                SqliteConnection.ClearAllPools();
                if (File.Exists(_dbPath)) File.Delete(_dbPath);
                foreach (var suffix in new[] { "-wal", "-shm" })
                {
                    var p = _dbPath + suffix;
                    if (File.Exists(p)) File.Delete(p);
                }
            }
            catch { /* best effort cleanup */ }
        }

        protected override async Task<long> CountPhysicalAsync()
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM \"People\"";
            var result = await command.ExecuteScalarAsync();
            return result is null ? 0 : Convert.ToInt64(result);
        }

        protected override async Task AssertIndexesCreatedAsync()
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='People' AND name NOT LIKE 'sqlite_%'";
            var names = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                names.Add(reader.GetString(0));

            Assert.Contains("ix_People_Name", names);
            Assert.Contains("ix_People_Age_State", names);
        }
    }
}
