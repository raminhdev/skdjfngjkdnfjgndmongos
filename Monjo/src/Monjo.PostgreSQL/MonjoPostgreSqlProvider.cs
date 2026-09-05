using System.Data.Common;

namespace Monjo.PostgreSQL
{
    /// <summary>PostgreSQL dialect: native boolean, timestamptz, uuid and NUMERIC mappings.</summary>
    public sealed class NpgsqlDialect : SqlDialect
    {
        public override string ProviderName => "PostgreSQL";
        public override string FalseLiteral => "FALSE";
        public override string TrueLiteral => "TRUE";
        public override bool SupportsNativeGuid => true;
        public override bool ReadsDateTimeAsText => false;
        public override bool ReadsDecimalAsText => false;

        public override string GetSqlType(Type clrType)
            => clrType switch
            {
                _ when clrType == typeof(string) => "TEXT",
                _ when clrType == typeof(bool) => "BOOLEAN",
                _ when clrType == typeof(int) => "INTEGER",
                _ when clrType == typeof(long) => "BIGINT",
                _ when clrType == typeof(short) => "SMALLINT",
                _ when clrType == typeof(byte) => "SMALLINT",
                _ when clrType == typeof(double) => "DOUBLE PRECISION",
                _ when clrType == typeof(float) => "REAL",
                // Full .NET decimal fidelity: 29 integer digits + 28 fraction digits
                // (decimal's maximum range/scale) — no silent truncation of user data.
                _ when clrType == typeof(decimal) => "NUMERIC(57,28)",
                _ when clrType == typeof(DateTime) => "TIMESTAMP WITH TIME ZONE",
                _ when clrType == typeof(Guid) => "UUID",
                _ when clrType == typeof(byte[]) => "BYTEA",
                _ when clrType.IsEnum => "TEXT",
                var other => throw new MonjoNotSupportedException(
                    $"CLR type '{other.Name}' has no PostgreSQL column type mapping in Monjo.")
            };

        public override DbConnection CreateConnection(MonjoOptions options)
        {
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(options.ConnectionString)
            {
                MaxPoolSize = options.PostgreSql.MaxPoolSize,
                MinPoolSize = options.PostgreSql.MinPoolSize,
                Timeout = options.PostgreSql.ConnectTimeoutSeconds,
                CommandTimeout = options.PostgreSql.CommandTimeoutSeconds,
            };
            return new Npgsql.NpgsqlConnection(builder.ConnectionString);
        }

        public override object ToDbValue(object value)
        {
            // Npgsql maps all supported CLR values natively (Guid → uuid, byte[] → BYTEA, ...).
            return value;
        }
    }

    /// <summary>
    /// Monjo's PostgreSQL provider. Npgsql's built-in pooling is used directly: every operation
    /// acquires/releases a pooled connection (no physical connect in the hot path), transactions
    /// hold one dedicated connection, and all I/O is fully asynchronous.
    /// </summary>
    public sealed class MonjoPostgreSqlProvider : SqlMonjoProvider
    {
        public override string Name => "PostgreSQL";

        public MonjoPostgreSqlProvider(MonjoOptions options)
            : base(options, new NpgsqlDialect(), options.PostgreSql.CommandTimeoutSeconds)
        {
        }
    }
}
