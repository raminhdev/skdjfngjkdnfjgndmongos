using Microsoft.AspNetCore.Http;

namespace Utilities.Middlewares
{
    public class DevelopmentCorsMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext httpContext)
        {
            addHeaders(httpContext);

            if (httpContext.Request.Method == "OPTIONS")
            {
                httpContext.Response.StatusCode = 200;

                await httpContext.Response.WriteAsync("OK");
            }
            else
            {
                await next(httpContext);
            }
        }

        public void addHeaders(HttpContext httpContext)
        {
            httpContext.Response.Headers.Append("Access-Control-Allow-Origin", new[] { "*" });
            httpContext.Response.Headers.Append("Access-Control-Allow-Headers", new[] {
                "Origin, X-Requested-With, Content-Type, Accept, Authorization","ApplicationId","Nonce","Signature"
            });
            httpContext.Response.Headers.Append("Access-Control-Allow-Methods", new[] { "GET, POST, PUT, DELETE, OPTIONS" });
            httpContext.Response.Headers.Append("Access-Control-Allow-Credentials", new[] { "true" });

            //Security Headers
            httpContext.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self'; font-src 'self'; img-src 'self'; frame-src 'self'");
            httpContext.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            httpContext.Response.Headers.Append("X-Frame-Options", "DENY");
            httpContext.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
            httpContext.Response.Headers.Remove("server");
        }
    }
}