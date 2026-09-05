using System.Data.Common;

namespace Monjo.Sql
{
    /// <summary>
    /// Per-provider SQL differences: identifier quoting is shared (double quotes, ANSI),
    /// but type mapping, boolean literals, Guid storage and connection creation are provider-specific.
    /// This is the ONLY place where "PostgreSQL" vs "SQLite" knowledge lives in the SQL engine.
    /// </summary>
    public abstract class SqlDialect
    {
        /// <summary>Canonical provider name.</summary>
        public abstract string ProviderName { get; }

        /// <summary>SQL literal for boolean false (PostgreSQL: FALSE, SQLite: 0).</summary>
        public abstract string FalseLiteral { get; }

        /// <summary>SQL literal for boolean true.</summary>
        public abstract string TrueLiteral { get; }

        /// <summary>True when the provider stores/reads Guid natively (PostgreSQL: uuid), false when stored as TEXT (SQLite).</summary>
        public abstract bool SupportsNativeGuid { get; }

        /// <summary>
        /// True when DateTime columns are stored as text and MUST be read as text and parsed as UTC
        /// (SQLite via Microsoft.Data.Sqlite: <c>GetDateTime</c> would misinterpret the stored UTC
        /// value as local time). False when <c>GetDateTime</c> is reliable (PostgreSQL timestamptz).
        /// </summary>
        public abstract bool ReadsDateTimeAsText { get; }

        /// <summary>SQL type name for a supported CLR column type.</summary>
        public abstract string GetSqlType(Type clrType);

        /// <summary>Creates a (pooled) connection configured from options. The connection is NOT opened here.</summary>
        public abstract DbConnection CreateConnection(MonjoOptions options);

        /// <summary>
        /// Final, dialect-specific write conversion applied after <see cref="SqlValueConverters.ToDb"/>:
        /// e.g. SQLite converts Guid to a TEXT value; PostgreSQL keeps the native Guid for the uuid column.
        /// </summary>
        public abstract object ToDbValue(object value);

        /// <summary>
        /// Maps provider-native exceptions onto Monjo's provider-independent ones where a mapping
        /// exists (e.g. SQLite busy/locked → <see cref="MonjoBusyException"/>). Returns null when
        /// the exception should be rethrown unchanged (the default — driver details are preserved).
        /// </summary>
        public virtual Exception? TranslateException(Exception exception) => null;

        /// <summary>Quoting rule shared by both providers (ANSI double quotes).</summary>
        public static string Quote(string identifier)
            => "\"" + identifier.Replace("\"", "\"\"") + "\"";
    }
}
