using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Utilities.Filters
{
    public class GzipCompressionAttribute : Attribute, IAsyncResultFilter
    {
        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            var result = context.Result as ObjectResult;
            if (result?.Value != null)
            {
                var acceptEncoding = context.HttpContext.Request.Headers["Accept-Encoding"];
                if (acceptEncoding.ToString().Contains("gzip"))
                {
                    context.HttpContext.Response.Headers.Append("Content-Encoding", "gzip");

                    using (var memoryStream = new MemoryStream())
                    {
                        using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
                        {
                            await next();
                            using (var writer = new StreamWriter(gzipStream, leaveOpen: true))
                            {
                                writer.Write(result.Value.ToString());
                            }
                        }

                        context.HttpContext.Response.Body = memoryStream;
                    }
                }
            }
            else
            {
                await next();
            }
        }
    }
}