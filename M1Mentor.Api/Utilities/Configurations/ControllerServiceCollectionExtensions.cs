using M1Mentor.Services._FileMeta.DTOs.Settings;
using M1Mentor.Utilities.Services;
using Microsoft.Extensions.Options;
using Utilities.Constants;
using Utilities.Models.Settings;
using Utilities.MongoDatabase;
using Utilities.MongoDatabase.Contracts;
using Utilities.Services;

namespace M1Mentor.Api.Utilities.Configurations
{
    public static class ControllerServiceCollectionExtensions
    {
        public static void AddSettings(this IServiceCollection services, IConfiguration configuration)
        {

            services.RegisterSetting<MonjoSettings, IMonjoSettings>(configuration.GetSection(nameof(MonjoSettings)));

            services.RegisterSetting<ApplicationPoolSettings>(configuration.GetSection(nameof(ApplicationPoolSettings)));

            services.RegisterSetting<JwtServiceSettings>(configuration.GetSection(nameof(JwtServiceSettings)));

            services.RegisterSetting<FirewallSettings>(configuration.GetSection(nameof(FirewallSettings)));

            services.RegisterSetting<CaptchaSettings>(configuration.GetSection(nameof(CaptchaSettings)));

            services.RegisterSetting<EmailSettings>(configuration.GetSection(nameof(EmailSettings)));

            services.RegisterSetting<AppSettings>(configuration.GetSection(nameof(AppSettings)));
            
            services.RegisterSetting<IRTHandlerSettings>(configuration.GetSection(nameof(IRTHandlerSettings)));

            services.RegisterSetting<FileSettings>(configuration.GetSection(nameof(FileSettings)));
            services.RegisterSetting<FileStorageSettings>(configuration.GetSection("FileStorage"));

            services.RegisterSetting<S3Settings>(configuration.GetSection(nameof(S3Settings)));
        }

        private static void RegisterSetting<TSettings>(this IServiceCollection services, IConfigurationSection configuration)
           where TSettings : class, new()
        {
            services.Configure<TSettings>(configuration);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<TSettings>>().Value);
        }

        private static void RegisterSetting<TSettings, TISettings>(this IServiceCollection services, IConfigurationSection configuration)
            where TISettings : class
            where TSettings : class, TISettings, new()
        {
            services.Configure<TSettings>(configuration);
            services.AddSingleton<TISettings>(sp => sp.GetRequiredService<IOptions<TSettings>>().Value);
        }
    }
}
