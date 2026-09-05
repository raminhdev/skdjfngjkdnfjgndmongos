using System.Reflection;
using Asp.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;
using Utilities.Extensions;
using Utilities.Swagger;

namespace Utilities.Configuration
{
    public static class SwaggerConfigurationExtensions
    {
        public static void AddSwagger(this IServiceCollection services)
        {
            services.NotNull(nameof(services));

            services.AddSwaggerExamples();

            services.AddSwaggerGen(options =>
            {
                options.EnableAnnotations();
                options.IgnoreObsoleteActions();
                options.IgnoreObsoleteProperties();
                options.UseInlineDefinitionsForEnums();
                options.ExampleFilters();

                options.SwaggerDoc("v1", new OpenApiInfo { Version = "v1", Title = "API V1" });
                options.SwaggerDoc("v2", new OpenApiInfo { Version = "v2", Title = "API V2" });

                #region Versioning
                options.OperationFilter<RemoveVersionParameters>();

                options.DocumentFilter<SetVersionInPaths>();

                options.DocInclusionPredicate((docName, apiDesc) =>
                {
                    if (!apiDesc.TryGetMethodInfo(out MethodInfo methodInfo)) return false;

                    var versions = methodInfo.DeclaringType
                        .GetCustomAttributes<ApiVersionAttribute>(true)
                        .SelectMany(attr => attr.Versions);

                    return versions.Any(v => $"v{v.ToString()}" == docName);
                });
                #endregion

                #region Add Headers
                var schemes = new List<OpenApiSecurityScheme>
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference {Type = ReferenceType.SecurityScheme, Id = "Bearer"},
                        Description = "JWT Token",
                        In = ParameterLocation.Header,
                        Name = "Authorization",
                        Type = SecuritySchemeType.ApiKey,
                        Scheme = "Bearer"
                    },
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference {Type = ReferenceType.SecurityScheme, Id = "ApplicationId"},
                        Description = "Application Id",
                        In = ParameterLocation.Header,
                        Name = "ApplicationId",
                        Type = SecuritySchemeType.ApiKey
                    },
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference {Type = ReferenceType.SecurityScheme, Id = "Nonce"},
                        Description = "Nonce",
                        In = ParameterLocation.Header,
                        Name = "Nonce",
                        Type = SecuritySchemeType.ApiKey
                    },
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference {Type = ReferenceType.SecurityScheme, Id = "Signature"},
                        Description = "Signature",
                        In = ParameterLocation.Header,
                        Name = "Signature",
                        Type = SecuritySchemeType.ApiKey
                    }
                };

                foreach (var scheme in schemes)
                {
                    options.AddSecurityDefinition(scheme.Reference.Id, scheme);
                    options.AddSecurityRequirement(new OpenApiSecurityRequirement() { { scheme, Array.Empty<string>() } });
                    //options.OperationFilter<ApplySecurityRequirementsToActionsFilter>(scheme, new List<Type> { typeof(AuthorizeAttribute) }, "Bearer");
                }

                options.SchemaGeneratorOptions = new SchemaGeneratorOptions { SchemaIdSelector = type => type.FullName };
                #endregion
            });
        }

        public static void UseSwaggerAndUI(this IApplicationBuilder app)
        {
            app.UseSwagger();

            app.UseSwaggerUI(options =>
            {
                options.DocExpansion(DocExpansion.None);

                options.SwaggerEndpoint("/swagger/v1/swagger.json", "V1 Docs");
                options.SwaggerEndpoint("/swagger/v2/swagger.json", "V2 Docs");
            });
        }
    }
}
