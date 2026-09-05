using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Utilities.Enums;
using Utilities.Models.Results;

namespace Utilities.Extensions
{
    public static class HttpContextExtensions
    {
        public static JwtSecurityToken GetToken(this HttpContext httpContext)
            => (JwtSecurityToken)httpContext.Items["Token"];

        public static string GetClaim(this HttpContext httpContext, string claim)
            => httpContext.GetToken()?.GetClaim(claim)?.Value;

        public static string GetRequestIpv4(this IHttpContextAccessor context)
        {
            var userIP = context.HttpContext?.GetRequestIpv4();
            if (!string.IsNullOrEmpty(userIP)) return userIP;
            return context.HttpContext?.Connection.RemoteIpAddress.ToString();
        }

        public static string GetRequestIpv4(this HttpContext context)
        {
            var userIP = context.Request.Headers.Where(q => q.Key == "X-Forwarded-For").Select(q => q.Value)
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(userIP)) return userIP;
            return context.Connection.RemoteIpAddress.ToString();
        }

        public static string GetRequestUserId(this IHttpContextAccessor context)
        {
            var token = context.HttpContext?.GetToken();
            if (token == null) return null;
            return token.GetClaim("UserId").Value;
        }

        public static async Task<string> GetRequestBodyStringAsync(this HttpRequest request)
        {
            request.EnableBuffering();

            var requestBodyString = await new StreamReader(request.Body).ReadToEndAsync();

            if (request.Headers.Values.Contains("multipart/form-data") ||
                string.IsNullOrWhiteSpace(requestBodyString)) return null;

            // Reset the request body stream position so the next middleware can read it
            request.Body.Position = 0;

            return requestBodyString;
        }

        public static async Task WriteToResponseAsync(this HttpContext context, string message,
            HttpStatusCode httpStatusCode, ApiResultStatusCode apiStatusCode)
        {
            if (context.Response.HasStarted)
                throw new InvalidOperationException("The response has already started, the http status code middleware will not be executed.");

            var result = new ApiResult(false, apiStatusCode, message);
            var json = JsonConvert.SerializeObject(result);

            context.Response.StatusCode = (int)httpStatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(json);
        }

        public static void SetUnAuthorizeResponse(this HttpContext context, Exception exception, IWebHostEnvironment env,
            ref HttpStatusCode httpStatusCode, ref ApiResultStatusCode apiStatusCode, ref string message)
        {
            httpStatusCode = HttpStatusCode.Unauthorized;
            apiStatusCode = ApiResultStatusCode.UnAuthorized;

            if (env.IsDevelopment())
            {
                var dic = new Dictionary<string, string>
                {
                    ["Exception"] = exception.Message,
                    ["StackTrace"] = exception.StackTrace
                };
                if (exception is SecurityTokenExpiredException tokenException)
                    dic.Add("Expires", tokenException.Expires.ToString());

                message = JsonConvert.SerializeObject(dic);
            }
        }
    }
}