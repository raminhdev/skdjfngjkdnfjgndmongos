using System.IO.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Utilities.Middlewares;

namespace Utilities.Configuration
{
    public static class ApplicationBuilderExtensions
    {
        public static void UseHsts(this IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (!env.IsDevelopment())
                app.UseHsts();
        }

        public static void UseDeveloperExceptionPage(this IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
        }

        public static void UseFile(this IApplicationBuilder app)
        {
            app.UseFileServer(new FileServerOptions
            {
                FileProvider = new PhysicalFileProvider(
                Path.Combine(Directory.GetCurrentDirectory(), "StaticFiles")), //Should Create This Folder Then Put The Code In Startup.cs
                RequestPath = "/StaticFiles", //Should Create This Folder Then Put The Code In Startup.cs
                EnableDefaultFiles = true
            });
        }

        public static void UseEndpoints(this IApplicationBuilder app)
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        public static void UseCustomExceptionHandler(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<CustomExceptionHandlerMiddleware>();
        }

        public static void UseCustomCors(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<DevelopmentCorsMiddleware>();
        }

        public static void UseFirewall(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<FirewallMiddleware>();
        }

        public static void UseSignature(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<SignatureMiddleware>();
        }

        public static void UseJwt(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<JwtMiddleware>();
        }

        public static void UseCustomGlobalRateLimiting(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<CustomGlobalRateLimitingMiddleware>();
        }

        public static void UseCustomRateLimiting(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<CustomRateLimitingMiddleware>();
        }

        public static void UseAntiXss(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<AntiXssMiddleware>();
        }

        public static void UseGzipResponseCompression(this IApplicationBuilder builder)
        {
            builder.UseResponseCompression();

            builder.Use(async (context, next) =>
            {
                var acceptEncoding = context.Request.Headers["Accept-Encoding"];
                if (acceptEncoding.ToString().Contains("gzip"))
                {
                    context.Response.Headers.Append("Content-Encoding", "gzip");
                    context.Response.Body = new GZipStream(context.Response.Body, CompressionMode.Compress);
                }

                await next();
            });
        }

        public static void UseCustomGzipResponseCompression(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<GzipCompressionMiddleware>();
        }

        /// <summary>
        /// this extension use for auto migration for SQL
        /// </summary>
        /// <param name="builder"></param>
        /// <exception cref="BaseException"></exception>
        //public static void UseDatabaseAutoMigrations(this IApplicationBuilder builder)
        //{
        //    using var scope = builder.ApplicationServices.CreateScope();
        //    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        //    try
        //    {
        //        MigrateDatabaseToLatestVersion.Execute(context, new DbMigrationsOptions
        //        {
        //            AutomaticMigrationDataLossAllowed = true,
        //            AutomaticMigrationsEnabled = true
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new BaseException("EF Core migration error");
        //    }
        //}

    }
}
