using System.IO.Compression;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Utilities.Constants;
using Utilities.Enums;
using Utilities.Exceptions;
using Utilities.Exceptions.Common;
using Utilities.Extensions;
using Utilities.Models.Settings;
using Utilities.Services;

namespace Utilities.Configuration
{
    public static class ServiceCollectionExtensions
    {
        public static void AddCustomControllers(this IServiceCollection services)
        {
            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            }).ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var messages = context.ModelState.Values
                        .Where(x => x.ValidationState == ModelValidationState.Invalid)
                        .SelectMany(x => x.Errors)
                        .Select(x => x.ErrorMessage)
                        .ToList();

                    throw new BaseException(ApiResultStatusCode.BadRequest, string.Join(" | ", messages), System.Net.HttpStatusCode.BadRequest);
                };
            });
        }

        public static void AddCustomApiVersioning(this IServiceCollection services)
        {
            services.AddApiVersioning(options =>
            {
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.ReportApiVersions = true;
            });
        }

        public static void AddCustomRateLimiter(this IServiceCollection services, int permitLimit = 60, int queueLimit = 0,
            TimeSpan? window = null)
        {
            //httpContext.Request.Headers.Host.ToString() for who not login yet
            services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.GetClaim("Publickey") ?? httpContext.GetRequestIpv4().Split(",")[0],
                        factory: partition => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = permitLimit,
                            QueueLimit = queueLimit,
                            Window = window ?? TimeSpan.FromMinutes(1)
                        }));

                options.OnRejected = (context, token) =>
                {
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                        throw new TooManyRequestsException(
                            $"Too many requests. Please try again after {retryAfter.TotalMinutes} minute(s)");
                    else
                        throw new TooManyRequestsException("Too many requests. Please try again later");

                };
            });
        }

        public static void AddMinimalMvc(this IServiceCollection services)
        {
            services.AddMvcCore(options =>
            {
                options.Filters.Add(new AuthorizeFilter());
            })
            .AddApiExplorer()
            .AddAuthorization()
            .AddFormatterMappings()
            .AddDataAnnotations()
            .AddCors();
        }

        public static void AddGzipResponseCompression(this IServiceCollection services)
        {
            services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

            services.AddResponseCompression(options =>
            {
                options.Providers.Add<GzipCompressionProvider>();
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                    new[]
                    {
                        "text/html",
                        "text/css",
                        "application/javascript",
                        "text/javascript"
                    });
            });
        }

        public static void AddCoreSettings(this IServiceCollection services, IConfiguration configuration)
        {
            // Monjo settings are bound by services.AddMonjo(configuration) into MonjoOptions,
            // which also reads the legacy "MonjoSettings" section for compatibility.

            services.RegisterSetting<ApplicationPoolSettings>(configuration.GetSection(nameof(ApplicationPoolSettings)));

            services.RegisterSetting<IRTHandlerSettings>(configuration.GetSection(nameof(IRTHandlerSettings)));

            services.RegisterSetting<JwtServiceSettings>(configuration.GetSection(nameof(JwtServiceSettings)));

            services.RegisterSetting<FirewallSettings>(configuration.GetSection(nameof(FirewallSettings)));

            services.RegisterSetting<CaptchaSettings>(configuration.GetSection(nameof(CaptchaSettings)));
            services.RegisterSetting<GhasedakConnectionSetting>(configuration.GetSection(nameof(GhasedakConnectionSetting)));

            //services.RegisterSetting<EmailSettings>(configuration.GetSection(nameof(EmailSettings)));

            //services.RegisterSetting<TelegramBotSettings>(configuration.GetSection(nameof(TelegramBotSettings)));
        }

        #region Private Methods

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

        #endregion
    }
}