using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Monjo
{
    /// <summary>
    /// Monjo DI entry point. Usage:
    /// <code>
    /// services.AddMonjo(configuration);          // binds MonjoOptions once (section "Monjo" or legacy "MonjoSettings")
    /// services.UseMonjoMongoDB();                // exactly one provider registration (from the provider package)
    /// </code>
    /// The provider is resolved ONCE (singleton <see cref="IMonjoProvider"/>); all repositories
    /// share its connection. No database call happens during registration.
    /// </summary>
    public static class MonjoServiceCollectionExtensions
    {
        /// <summary>Binds options and registers the provider-resolution pipeline.</summary>
        public static IServiceCollection AddMonjo(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = MonjoOptions.DefaultSectionName)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var options = MonjoOptions.Bind(configuration, sectionName);
            services.AddSingleton(options);
            services.AddSingleton<IMonjoProvider>(provider =>
            {
                var factory = MonjoProviderRegistration.GetFactory(provider);
                if (factory is null)
                    throw new MonjoProviderNotRegisteredException(
                        $"Monjo is configured to use the '{options.Provider}' provider, but no provider factory is registered. " +
                        "Reference the matching provider package and register it: services.UseMonjoMongoDB(), " +
                        "services.UseMonjoPostgreSql() or services.UseMonjoSqlite().");
                if (!string.Equals(factory.ProviderName, options.Provider, StringComparison.OrdinalIgnoreCase))
                    throw new MonjoProviderNotRegisteredException(
                        $"Configuration selects provider '{options.Provider}' but the registered provider is '{factory.ProviderName}'. " +
                        "Update the 'Monjo:Provider' configuration value or register the matching provider.");
                return factory.Factory(provider, options);
            });
            services.AddSingleton<IMonjoConnection>(provider => provider.GetRequiredService<IMonjoProvider>().Connection);
            return services;
        }
    }
}
