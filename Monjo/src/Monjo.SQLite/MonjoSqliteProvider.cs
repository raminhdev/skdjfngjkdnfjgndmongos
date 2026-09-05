using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace Monjo.SQLite
{
    /// <summary>
    /// SQLite dialect. Type affinity per SQLite conventions; booleans as INTEGER,
    /// DateTime/decimal as TEXT (Microsoft.Data.Sqlite's default handling), Guid as TEXT.
    /// </summary>
    public sealed class SqliteDialect : SqlDialect
    {
        public override string ProviderName => "SQLite";
        public override string FalseLiteral => "0";
        public override string TrueLiteral => "1";
        public override bool SupportsNativeGuid => false;
        public override bool ReadsDateTimeAsText => true;

        public override string GetSqlType(Type clrType)
            => clrType switch
            {
                _ when clrType == typeof(string) => "TEXT",
                _ when clrType == typeof(bool) => "INTEGER",
                _ when clrType == typeof(int) => "INTEGER",
                _ when clrType == typeof(long) => "INTEGER",
                _ when clrType == typeof(short) => "INTEGER",
                _ when clrType == typeof(byte) => "INTEGER",
                _ when clrType == typeof(double) => "REAL",
                _ when clrType == typeof(float) => "REAL",
                _ when clrType == typeof(decimal) => "TEXT",
                _ when clrType == typeof(DateTime) => "TEXT",
                _ when clrType == typeof(Guid) => "TEXT",
                _ when clrType == typeof(byte[]) => "BLOB",
                _ when clrType.IsEnum => "TEXT",
                var other => throw new MonjoNotSupportedException(
                    $"CLR type '{other.Name}' has no SQLite column type mapping in Monjo.")
            };

        public override DbConnection CreateConnection(MonjoOptions options)
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = options.ConnectionString,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = true,
                DefaultTimeout = options.Sqlite.BusyTimeoutSeconds,
            };
            return new SqliteConnection(builder.ConnectionString);
        }

        public override object ToDbValue(object value)
            => value is Guid guid ? guid.ToString("N") : value;

        /// <summary>Translates SQLite busy/locked failures into the provider-independent <see cref="MonjoBusyException"/>.</summary>
        public override Exception? TranslateException(Exception exception)
            => exception is SqliteException { SqliteErrorCode: SqliteErrorCode.Busy or SqliteErrorCode.Locked } e
                ? new MonjoBusyException(
                    "SQLite is busy: another writer holds the database lock. Increase 'Monjo:Sqlite:BusyTimeoutSeconds' " +
                    "or serialize writers (SQLite supports concurrent readers but a single writer).", e)
                : null;
    }

    /// <summary>
    /// Monjo's SQLite provider. Microsoft.Data.Sqlite connection pooling + WAL journal mode +
    /// busy timeout: reads stay concurrent, writers are serialized by SQLite itself (documented
    /// behavior for embedded workloads).
    /// </summary>
    public sealed class MonjoSqliteProvider : SqlMonjoProvider
    {
        public override string Name => "SQLite";

        public MonjoSqliteProvider(MonjoOptions options)
            : base(options, new SqliteDialect(), options.Sqlite.BusyTimeoutSeconds + 15)
        {
        }

        /// <summary>The SQLite file (connection string) is the database identity.</summary>
        internal override string DatabaseIdentity => Options.ConnectionString;

        /// <inheritdoc/>
        protected override async Task EnsureProviderPragmasAsync(CancellationToken cancellationToken)
        {
            // WAL is persistent; setting it again is a cheap no-op. Done once per process
            // (the readiness gate ensures that), never per request.
            await ExecuteDdlAsync("PRAGMA journal_mode=WAL;", cancellationToken).ConfigureAwait(false);
        }
    }
}
