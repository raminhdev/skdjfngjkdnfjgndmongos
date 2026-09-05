using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Utilities.Swagger
{
    public class AddAdditionalResponseExampleFilter(Responses[] responses) : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            foreach (var response in responses)
            {
                if (response == Responses.badRequest)
                    operation.Responses.TryAdd("400", new OpenApiResponse { Description = "Bad Request" });
                else if (response == Responses.forbidden)
                    operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden" });
                else if (response == Responses.unauthorized)
                    operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });
                else if (response == Responses.internalServerError)
                    operation.Responses.TryAdd("500", new OpenApiResponse { Description = "Internal Server Error" });
                else if (response == Responses.badGateway)
                    operation.Responses.TryAdd("502", new OpenApiResponse { Description = "Bad Gateway" });
                else
                    operation.Responses.TryAdd("504", new OpenApiResponse { Description = "Gateway Timeout" });
            }
        }
    }
    public enum Responses { badRequest, unauthorized, forbidden, internalServerError, badGateway, gatewayTimeout }
}
