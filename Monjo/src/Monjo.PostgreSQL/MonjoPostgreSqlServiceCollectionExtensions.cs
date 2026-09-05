using Microsoft.Extensions.DependencyInjection;

namespace Monjo.PostgreSQL
{
    /// <summary>Registers the Monjo PostgreSQL provider.</summary>
    /// <example>
    /// <code>
    /// services.AddMonjo(configuration);        // Monjo.Core
    /// services.UseMonjoPostgreSql();           // this package
    /// </code>
    /// </example>
    public static class MonjoPostgreSqlServiceCollectionExtensions
    {
        public const string ProviderName = "PostgreSQL";

        public static IServiceCollection UseMonjoPostgreSql(this IServiceCollection services)
            => services.AddMonjoProviderFactory(ProviderName, (_, options) => new MonjoPostgreSqlProvider(options));
    }
}
