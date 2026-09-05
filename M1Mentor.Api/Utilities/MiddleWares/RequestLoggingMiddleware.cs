using System.Text;
using System.Text.Json;
using M1Mentor.Services._Log;
using M1Mentor.Services._Log.DTOs.Updates;
using Utilities.Enums;
using Utilities.Extensions;
using Utilities.Services.Contracts;

namespace M1Mentor.Api.Utilities.MiddleWares
{
    public class RequestLoggingMiddleware(RequestDelegate _next, ILogService _logService, IJwtService _jwtService)
    {
        // Hard ceiling for how much request body we are willing to persist into Mongo.
        // Mongo's BSON document limit is 16 MB; we stay far below it so a single RequestLog
        // document can never exceed the limit, regardless of upload size.
        private const int MaxLoggedBodyBytes = 32 * 1024; // 32 KB

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Path.StartsWithSegments("/api"))
            {
                await _next(context);
                return;
            }

            var body = await CaptureBodyAsync(context.Request);

            var headers = JsonSerializer.Serialize(context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));
            var query = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;
            var segments = context.Request.Path.HasValue ? context.Request.Path.Value.Split("/", StringSplitOptions.RemoveEmptyEntries) : null;
            var ip = context.GetRequestIpv4();

            var update = new RequestLogUpdate
            {
                ControllerName = (segments != null && segments.Length > 2) ? segments[2] : null,
                ApiName = (segments != null && segments.Length > 3) ? segments[3] : null,
                Body = body,
                Headers = headers,
                Query = string.IsNullOrWhiteSpace(query) ? null : query,
                RoutePath = context.Request.Path,
                ClientIP = ip?.Split(",")[0]
            };

            string publicKey = "anonymous";
            string phone = "anonymous";

            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            if (!string.IsNullOrWhiteSpace(token))
            {
                try
                {
                    var jwtToken = _jwtService.Validate(token);
                    if (jwtToken != null)
                    {
                        publicKey = jwtToken.Claims.FirstOrDefault(c => c.Type == Claims.PublicKey.ToDisplay())?.Value ?? "anonymous";
                        phone = jwtToken.Claims.FirstOrDefault(c => c.Type == Claims.PhoneNumber.ToDisplay())?.Value ?? "anonymous";
                    }
                }
                catch
                {
                }
            }
            await _logService.CaptureRequestLogAsync(update, publicKey, phone);

            await _next(context);
        }

        /// <summary>
        /// Safely captures a bounded textual representation of the request body for logging.
        /// File uploads (multipart/form-data, octet-stream, media types, …) are NEVER read into
        /// memory or persisted — only lightweight metadata (file name, content type, length) is
        /// recorded. Textual bodies are truncated to <see cref="MaxLoggedBodyBytes"/> so a request
        /// log document can never breach Mongo's 16 MB document limit. This is the fix for the
        /// historical "Size N is larger than MaxDocumentSize 16777216" failure on file uploads.
        /// </summary>
        private static async Task<string> CaptureBodyAsync(HttpRequest request)
        {
            var contentType = request.ContentType ?? string.Empty;

            // Binary / upload payloads: log metadata only, never the bytes.
            if (IsBinaryOrUpload(contentType))
            {
                if (request.HasFormContentType)
                {
                    // ReadFormAsync streams large files to a temp buffer on disk (Kestrel default),
                    // so this does not load the upload into memory. We only keep tiny metadata.
                    try
                    {
                        var form = await request.ReadFormAsync();
                        var files = form.Files
                            .Select(f => new { f.Name, f.FileName, f.ContentType, f.Length })
                            .ToList();

                        return JsonSerializer.Serialize(new
                        {
                            type = "multipart/form-data",
                            contentLength = request.ContentLength,
                            fields = form.Keys.Where(k => form.Files.All(f => f.Name != k)).ToArray(),
                            files
                        });
                    }
                    catch
                    {
                        // Fall through to generic metadata if the form cannot be parsed.
                    }
                }

                return JsonSerializer.Serialize(new
                {
                    type = "binary",
                    contentType,
                    contentLength = request.ContentLength
                });
            }

            // Textual payloads (JSON, form-urlencoded, plain text): capture a bounded prefix only.
            request.EnableBuffering();

            var buffer = new byte[MaxLoggedBodyBytes];
            var read = 0;
            while (read < buffer.Length)
            {
                var chunk = await request.Body.ReadAsync(buffer.AsMemory(read, buffer.Length - read));
                if (chunk == 0) break;
                read += chunk;
            }
            request.Body.Position = 0;

            var text = Encoding.UTF8.GetString(buffer, 0, read);

            if ((request.ContentLength ?? read) > MaxLoggedBodyBytes)
                text += $"…[truncated, total {request.ContentLength ?? read} bytes]";

            return text;
        }

        private static bool IsBinaryOrUpload(string contentType)
        {
            if (string.IsNullOrEmpty(contentType)) return false;

            contentType = contentType.ToLowerInvariant();

            return contentType.StartsWith("multipart/")
                || contentType.StartsWith("application/octet-stream")
                || contentType.StartsWith("image/")
                || contentType.StartsWith("video/")
                || contentType.StartsWith("audio/")
                || contentType.StartsWith("application/zip")
                || contentType.StartsWith("application/pdf");
        }
    }
}
