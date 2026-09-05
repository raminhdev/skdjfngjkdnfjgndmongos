using Microsoft.Extensions.DependencyInjection;

namespace Monjo.SQLite
{
    /// <summary>Registers the Monjo SQLite provider.</summary>
    /// <example>
    /// <code>
    /// services.AddMonjo(configuration);        // Monjo.Core
    /// services.UseMonjoSqlite();               // this package
    /// </code>
    /// </example>
    public static class MonjoSqliteServiceCollectionExtensions
    {
        public const string ProviderName = "SQLite";

        public static IServiceCollection UseMonjoSqlite(this IServiceCollection services)
            => services.AddMonjoProviderFactory(ProviderName, (_, options) => new MonjoSqliteProvider(options));
    }
}
