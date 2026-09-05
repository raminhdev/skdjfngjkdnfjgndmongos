using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Utilities.Swagger
{
    class ApplySecurityRequirementsToActionsFilter(
        OpenApiSecurityScheme scheme,
        List<Type> types,
        string schemeName = "Bearer")
        : IOperationFilter
    {
        #region Constructor
        private readonly string _schemeName = schemeName;

        #endregion 

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var filters = context.ApiDescription.ActionDescriptor.FilterDescriptors;

            foreach (var attribute in types)
            {
                if (filters.All(q => q.Filter.GetType().FullName != attribute.FullName))
                {
                    return;
                }
            }

            operation.Security = new List<OpenApiSecurityRequirement>
            {
                new OpenApiSecurityRequirement { { scheme, Array.Empty<string>() } }
            };
        }
    }
}