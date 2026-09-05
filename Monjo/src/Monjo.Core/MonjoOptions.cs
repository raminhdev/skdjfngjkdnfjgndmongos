using Microsoft.Extensions.Configuration;

namespace Monjo
{
    /// <summary>
    /// All Monjo settings, bound ONCE at startup from configuration (no per-request parsing).
    /// </summary>
    /// <remarks>
    /// Configuration shape (new):
    /// <code>
    /// "Monjo": {
    ///   "Database": { "Provider": "PostgreSQL" },   // or "Provider" directly under "Monjo"
    ///   "ConnectionString": "Host=...",
    ///   "DatabaseName": "app",
    ///   "AutoCreateSchema": true,    // SQL providers: CREATE TABLE IF NOT EXISTS (once, lazily)
    ///   "AutoCreateIndexes": true,   // indexes declared via [MonjoIndex] (once, lazily)
    ///   "PostgreSql": { "MaxPoolSize": 100, "MinPoolSize": 0, "ConnectTimeoutSeconds": 15, "CommandTimeoutSeconds": 30 },
    ///   "Sqlite": { "BusyTimeoutSeconds": 5 },
    ///   "Mongo": { "ServerMonitoringMode": "Poll", "MaxConnecting": 1 }
    /// }
    /// </code>
    /// Legacy shape (kept for compatibility): a <c>"MonjoSettings"</c> section with
    /// <c>ConnectionString</c>/<c>DatabaseName</c> is used when no <c>"Monjo"</c> section exists,
    /// and the provider defaults to MongoDB.
    /// </remarks>
    public sealed class MonjoOptions
    {
        public const string DefaultSectionName = "Monjo";
        public const string LegacySectionName = "MonjoSettings";

        /// <summary>Provider key: "MongoDB", "PostgreSQL" or "SQLite" (canonical form).</summary>
        public string Provider { get; set; } = "MongoDB";

        /// <summary>Provider-native connection string.</summary>
        public string ConnectionString { get; set; }

        /// <summary>Database (schema) name.</summary>
        public string DatabaseName { get; set; }

        /// <summary>SQL providers: create the table when missing (idempotent, once per process).</summary>
        public bool AutoCreateSchema { get; set; } = true;

        /// <summary>Create indexes declared via <c>[MonjoIndex]</c> when missing (idempotent, once per process).</summary>
        public bool AutoCreateIndexes { get; set; } = true;

        public MonjoMongoOptions Mongo { get; } = new();
        public MonjoPostgreSqlOptions PostgreSql { get; } = new();
        public MonjoSqliteOptions Sqlite { get; } = new();

        /// <summary>Binds options from configuration. Tries <paramref name="sectionName"/> first, then the legacy section.</summary>
        public static MonjoOptions Bind(IConfiguration configuration, string sectionName = DefaultSectionName)
        {
            var section = configuration.GetSection(sectionName);
            if (!section.GetChildren().Any())
                section = configuration.GetSection(LegacySectionName);

            var options = new MonjoOptions
            {
                Provider = NormalizeProviderName(section["Database:Provider"] ?? section["Provider"]),
                ConnectionString = section["ConnectionString"],
                DatabaseName = section["DatabaseName"],
                AutoCreateSchema = ParseBool(section["AutoCreateSchema"], true),
                AutoCreateIndexes = ParseBool(section["AutoCreateIndexes"], true),
            };

            options.Mongo.ServerMonitoringMode = section["Mongo:ServerMonitoringMode"] ?? "Poll";
            options.Mongo.MaxConnecting = ParseInt(section["Mongo:MaxConnecting"], 1);
            options.Mongo.ConnectTimeoutSeconds = ParseInt(section["Mongo:ConnectTimeoutSeconds"], 30);
            options.Mongo.ServerSelectionTimeoutSeconds = ParseInt(section["Mongo:ServerSelectionTimeoutSeconds"], 30);
            options.Mongo.HeartbeatIntervalSeconds = ParseInt(section["Mongo:HeartbeatIntervalSeconds"], 20);
            options.Mongo.EnforceTls12 = ParseBool(section["Mongo:EnforceTls12"], true);

            options.PostgreSql.MaxPoolSize = ParseInt(section["PostgreSql:MaxPoolSize"], 100);
            options.PostgreSql.MinPoolSize = ParseInt(section["PostgreSql:MinPoolSize"], 0);
            options.PostgreSql.ConnectTimeoutSeconds = ParseInt(section["PostgreSql:ConnectTimeoutSeconds"], 15);
            options.PostgreSql.CommandTimeoutSeconds = ParseInt(section["PostgreSql:CommandTimeoutSeconds"], 30);

            options.Sqlite.BusyTimeoutSeconds = ParseInt(section["Sqlite:BusyTimeoutSeconds"], 5);

            return options;
        }

        /// <summary>
        /// Maps the many accepted provider spellings onto the canonical name; unknown names are
        /// passed through (trimmed) so resolution can produce a precise error. An absent/empty
        /// value means the legacy configuration (no provider key), which historically means
        /// MongoDB.
        /// </summary>
        public static string NormalizeProviderName(string? name)
        {
            var trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                return "MongoDB";
            return trimmed.ToLowerInvariant() switch
            {
                "mongodb" or "mongo" => "MongoDB",
                "postgresql" or "postgres" or "pg" => "PostgreSQL",
                "sqlite" or "sqlite3" => "SQLite",
                _ => trimmed
            };
        }

        private static bool ParseBool(string? value, bool fallback)
            => value is not null && bool.TryParse(value, out var b) ? b : fallback;

        private static int ParseInt(string? value, int fallback)
            => value is not null && int.TryParse(value, out var i) ? i : fallback;
    }

    /// <summary>MongoDB provider options (defaults preserve the pre-existing MonjoConnection behaviour).</summary>
    public sealed class MonjoMongoOptions
    {
        /// <summary>"Poll" (short-lived heartbeat connections; avoids the macOS SecureTransport issue) or "Stream".</summary>
        public string ServerMonitoringMode { get; set; } = "Poll";
        public int MaxConnecting { get; set; } = 1;
        public int ConnectTimeoutSeconds { get; set; } = 30;
        public int ServerSelectionTimeoutSeconds { get; set; } = 30;
        public int HeartbeatIntervalSeconds { get; set; } = 20;
        public bool EnforceTls12 { get; set; } = true;
    }

    /// <summary>PostgreSQL provider options (Npgsql pooling).</summary>
    public sealed class MonjoPostgreSqlOptions
    {
        public int MaxPoolSize { get; set; } = 100;
        public int MinPoolSize { get; set; }
        public int ConnectTimeoutSeconds { get; set; } = 15;
        public int CommandTimeoutSeconds { get; set; } = 30;
    }

    /// <summary>SQLite provider options.</summary>
    public sealed class MonjoSqliteOptions
    {
        /// <summary>How long (seconds) a writer waits for the SQLite write lock before failing with <see cref="MonjoBusyException"/>.</summary>
        public int BusyTimeoutSeconds { get; set; } = 5;
    }
}
