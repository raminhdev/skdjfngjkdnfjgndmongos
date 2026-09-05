using System.IO.Compression;
using Microsoft.AspNetCore.Http;

namespace Utilities.Middlewares
{
    public class GzipCompressionMiddleware(RequestDelegate _next)
    {
        public async Task Invoke(HttpContext context)
        {
            var acceptEncoding = context.Request.Headers["Accept-Encoding"];

            if (acceptEncoding.ToString().Contains("gzip"))
            {
                context.Response.Headers.Append("Content-Encoding", "gzip");
                context.Response.Body = new GZipStream(context.Response.Body, CompressionLevel.Optimal);
            }

            await _next(context);
        }
    }
}
