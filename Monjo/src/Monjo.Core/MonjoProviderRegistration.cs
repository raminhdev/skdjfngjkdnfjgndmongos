using Microsoft.Extensions.DependencyInjection;

namespace Monjo
{
    /// <summary>
    /// Central provider registration. Provider packages (Monjo.MongoDB, Monjo.PostgreSQL,
    /// Monjo.SQLite, future providers) contribute a named factory here; <c>AddMonjo</c> resolves
    /// the configured provider through this registry ONCE at startup. No assembly scanning,
    /// no per-request lookup, no provider construction at registration time.
    /// </summary>
    public static class MonjoProviderRegistration
    {
        internal static readonly Type FactoryServiceType =
            typeof(MonjoProviderFactoryRecord);

        /// <summary>
        /// Registers the factory that builds the named provider. A second registration throws
        /// immediately: a process may use exactly one Monjo provider.
        /// </summary>
        public static IServiceCollection AddMonjoProviderFactory(
            this IServiceCollection services,
            string providerName,
            Func<IServiceProvider, MonjoOptions, IMonjoProvider> factory)
        {
            ArgumentException.ThrowIfNullOrEmpty(providerName);
            ArgumentNullException.ThrowIfNull(factory);

            if (services.Any(d => d.ServiceType == FactoryServiceType))
                throw new MonjoException(
                    "Only one Monjo database provider can be registered per application. " +
                    $"Refusing to add provider '{providerName}'.");

            services.AddSingleton(FactoryServiceType, _ => new MonjoProviderFactoryRecord(providerName, factory));
            return services;
        }

        internal static MonjoProviderFactoryRecord? GetFactory(IServiceProvider provider)
            => provider.GetService(FactoryServiceType) as MonjoProviderFactoryRecord;
    }

    /// <summary>Holds the registered provider name and its factory. Created once at startup.</summary>
    public sealed class MonjoProviderFactoryRecord(
        string ProviderName,
        Func<IServiceProvider, MonjoOptions, IMonjoProvider> Factory)
    {
        public string ProviderName { get; } = ProviderName;
        public Func<IServiceProvider, MonjoOptions, IMonjoProvider> Factory { get; } = Factory;
    }
}
