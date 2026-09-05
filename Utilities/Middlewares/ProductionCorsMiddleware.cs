using Microsoft.AspNetCore.Http;
using Utilities.Exceptions;

namespace Utilities.Middlewares
{
    public class ProductionCorsMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext httpContext)
        {
            AddCorsHeaders(httpContext);

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

        private void AddCorsHeaders(HttpContext httpContext)
        {
            //var origin = httpContext.Request.Headers["Origin"].ToString();

            //// Ensure that AllowCredentials works by setting a specific origin (not *)
            //if (!string.IsNullOrEmpty(origin))
            //{
            //    // Allow credentials requires a specific origin (not wildcard)
            //    httpContext.Response.Headers.Append("Access-Control-Allow-Origin", origin);
            var origin = httpContext.Request.Headers["Origin"].ToString();

            // Check if the Origin matches allowed domains
            if (origin == "https://test.com" || origin == "https://mp.test.com" || origin == "https://api.test.com")
            {
                httpContext.Response.Headers.Append("Access-Control-Allow-Origin", origin);
                httpContext.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
            }
            else
            {
                httpContext.Response.StatusCode = 403; // Forbidden
                httpContext.Response.WriteAsync("Access Denied");
                throw new AuthorizationException("Access Denied");
            }

            //httpContext.Response.Headers.Append("Access-Control-Allow-Origin", new[] { "*" });
            httpContext.Response.Headers.Append("Access-Control-Allow-Headers",
                "x-signalr-user-agent, Origin, X-Requested-With, Content-Type, Accept, Authorization, ApplicationId, Nonce, Signature");
            httpContext.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
            //httpContext.Response.Headers.Append("Access-Control-Allow-Credentials", "true");

            // Optional: Add security headers
            //httpContext.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self'; font-src 'self'; img-src 'self'; frame-src 'self'");
            httpContext.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            httpContext.Response.Headers.Append("X-Frame-Options", "DENY");
            httpContext.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
            httpContext.Response.Headers.Append("Content-Security-Policy", "frame-ancestors 'self' https://test.com https://panel.test.com https://api.test.com");
            httpContext.Response.Headers.Remove("server");
        }
    }
}