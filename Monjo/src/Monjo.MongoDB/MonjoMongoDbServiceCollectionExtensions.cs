using Microsoft.Extensions.DependencyInjection;

namespace Monjo.MongoDB
{
    /// <summary>Registers the Monjo MongoDB provider (plus the legacy connection surface).</summary>
    /// <example>
    /// <code>
    /// services.AddMonjo(configuration);        // Monjo.Core
    /// services.UseMonjoMongoDB();              // this package
    /// </code>
    /// </example>
    public static class MonjoMongoDbServiceCollectionExtensions
    {
        public const string ProviderName = "MongoDB";

        public static IServiceCollection UseMonjoMongoDB(this IServiceCollection services)
        {
            services.AddMonjoProviderFactory(ProviderName, (_, options) => new MongoMonjoProvider(options));

            // The provider's connection, exposed both as the concrete type and as the legacy
            // Utilities.MongoDatabase.Contracts.IMonjoConnection (Client/Database) for
            // pre-existing application code.
            services.AddSingleton(sp => (MongoMonjoProvider)sp.GetRequiredService<IMonjoProvider>());
            services.AddSingleton<MongoMonjoConnection>(sp => sp.GetRequiredService<MongoMonjoProvider>().Connection);
            services.AddSingleton<Utilities.MongoDatabase.Contracts.IMonjoConnection>(
                sp => sp.GetRequiredService<MongoMonjoConnection>());

            return services;
        }
    }
}
