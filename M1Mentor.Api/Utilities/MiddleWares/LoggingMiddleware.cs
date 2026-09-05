using M1Mentor.Services._Log;
using M1Mentor.Services._Log.DTOs.Updates;
using System.Text;
using System.Text.Json;
using Utilities.Attributes;
using Utilities.Constants;
using Utilities.Extensions;

namespace M1Mentor.Api.Utilities.Middlewares
{
    public class LoggingMiddleware(RequestDelegate _next, ILogService _logService)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            var ignoreLog = endpoint?.Metadata.GetMetadata<IgnoreLoggingAttribute>();
            if (ignoreLog != null)
            {
                await _next(context);
                return;
            }

            var request = context.Request;

            var headers = JsonSerializer.Serialize(
                request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));

            var query = request.QueryString.HasValue ? request.QueryString.Value : null;
            var path = request.Path.Value;
            var segments = path?.Split("/", StringSplitOptions.RemoveEmptyEntries);
            var ip = context.GetRequestIpv4()?.Split(",")[0];

            var user = CurrentRequestContext.User;
            var publicKey = user?.PublicKey ?? "anonymous";
            var phone = "anonymous";

            var log = new RequestLogUpdate
            {
                ControllerName = (segments != null && segments.Length > 2) ? segments[2] : null,
                ApiName = (segments != null && segments.Length > 3) ? segments[3] : null,
                Headers = headers,
                Query = string.IsNullOrWhiteSpace(query) ? null : query,
                RoutePath = path,
                ClientIP = ip
            };


            bool isMultipart = request.ContentType?.StartsWith("multipart/form-data") == true;
            bool hasBody = request.ContentLength.HasValue && request.ContentLength > 0;

            // Suspicious: Upload endpoint but not multipart
            if (path.Contains("/upload", StringComparison.OrdinalIgnoreCase) && hasBody && !isMultipart)
            {
                log.Body = "[⚠️ Suspicious] Non-multipart upload attempt";
                await _logService.CaptureRequestLogAsync(log, publicKey, phone);

                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                await context.Response.WriteAsync("Only multipart/form-data is allowed.");
                return;
            }

            // Suspicious: Huge query string (possible abuse)
            if (query != null && query.Length > 2048)
            {
                log.Body = "[⚠️ Suspicious] Query string too large";
                await _logService.CaptureRequestLogAsync(log, publicKey, phone);

                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Query too large.");
                return;
            }

            // Suspicious: Large headers (possible header abuse)
            if (request.Headers.Sum(h => h.Key.Length + h.Value.ToString().Length) > 32 * 1024)
            {
                log.Body = "[⚠️ Suspicious] Headers too large";
                await _logService.CaptureRequestLogAsync(log, publicKey, phone);

                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Headers too large.");
                return;
            }

            try
            {
                if (isMultipart)
                {
                    log.Body = JsonSerializer.Serialize(new
                    {
                        Type = "FileUpload",
                        Note = "Multipart form data"
                    });
                }
                else if (hasBody && request.ContentLength < 1024 * 1024 * 2) // < 1MB
                {
                    request.EnableBuffering();

                    using var reader = new StreamReader(
                        request.Body,
                        Encoding.UTF8,
                        detectEncodingFromByteOrderMarks: false,
                        bufferSize: 1024,
                        leaveOpen: true);

                    request.Body.Position = 0;
                    log.Body = await reader.ReadToEndAsync();
                    request.Body.Position = 0;
                }
                else if (hasBody)
                {
                    log.Body = "[Body skipped due to size]";
                }
            }
            catch
            {
                log.Body = "[Body read error]";
            }

            await _logService.CaptureRequestLogAsync(log, publicKey, phone);

            await _next(context);

            // we can have some post-processing here if needed, like logging response status or time taken and edit the log entry that we inserted 
        }

        //public async Task InvokeAsync(HttpContext context)
        //{
        //    //Does not work GetEndpoint returns null when the endpoint is upload in file service don't know why
        //    var ignoreLog = context.GetEndpoint()?.Metadata.GetMetadata<IgnoreLogingAttribute>();
        //    if (ignoreLog != null)
        //    {
        //        await _next(context);
        //        return;
        //    }

        //    context.Request.EnableBuffering();

        //    using var reader = new StreamReader(
        //        context.Request.Body,
        //        encoding: Encoding.UTF8,
        //        detectEncodingFromByteOrderMarks: false,
        //        bufferSize: 1024,
        //        leaveOpen: true);

        //    context.Request.Body.Position = 0;

        //    var headers = JsonSerializer.Serialize(context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));
        //    var query = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;
        //    var segments = context.Request.Path.HasValue ? context.Request.Path.Value.Split("/", StringSplitOptions.RemoveEmptyEntries) : null;
        //    var ip = context.GetRequestIpv4();

        //    var user = CurrentRequestContext.User;

        //    var update = new LogUpdate
        //    {
        //        ControllerName = (segments != null && segments.Length > 2) ? segments[2] : null,
        //        ApiName = (segments != null && segments.Length > 3) ? segments[3] : null,
        //        Headers = headers,
        //        Query = string.IsNullOrWhiteSpace(query) ? null : query,
        //        RoutePath = context.Request.Path,
        //        ClientIP = ip?.Split(",")[0],
        //    };

        //    if (context.Request.HasFormContentType && context.Request.Form.Files.Count > 0)
        //    {
        //        var files = context.Request.Form.Files;

        //        update.Body = JsonSerializer.Serialize(new
        //        {
        //            Type = "FileUpload",
        //            User = user?.PublicKey,
        //            FileCount = files.Count,
        //            Files = files.Select(f => new
        //            {
        //                f.FileName,
        //                f.Length,
        //                f.ContentType
        //            })
        //        });
        //    }
        //    else if (context.Request.ContentLength < 1024 * 1024)
        //    {
        //        update.Body = await reader.ReadToEndAsync();
        //        context.Request.Body.Position = 0;
        //    }
        //    else
        //    {
        //        update.Body = "[Body skipped due to size]";
        //    }

        //    await _logService.CaptureLogAsync(update);

        //    await _next(context);
        //}
    }
}
