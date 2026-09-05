using Microsoft.AspNetCore.Http;
using Utilities.Attributes;
using Utilities.Utilities;

namespace Utilities.Middlewares
{
    public class AntiXssMiddleware(RequestDelegate _next)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var securityAttribute = context.GetEndpoint()?.Metadata.GetMetadata<SecurityAttribute>();

            if (securityAttribute != null && !securityAttribute.Disable)
            {
                context.Request.Path = HtmlSanitizer.Sanitize(context.Request.Path, securityAttribute.EncodeInputs, securityAttribute.IgnoreLinks);

                foreach (var key in context.Request.Query.Keys)
                {
                    context.Request.Query = new QueryCollection(
                        context.Request.Query.ToDictionary(q => q.Key,
                            q => (Microsoft.Extensions.Primitives.StringValues)HtmlSanitizer.Sanitize(q.Value, securityAttribute.EncodeInputs, securityAttribute.IgnoreLinks))
                    );
                }

                //foreach (var key in context.Request.Headers.Keys)
                //{
                //    context.Request.Headers[key] = HtmlSanitizer.Sanitize(context.Request.Headers[key], securityAttribute.EncodeInputs, securityAttribute.AllowLinks);
                //}

                if (context.Request.Method == HttpMethods.Post || context.Request.Method == HttpMethods.Put)
                {
                    context.Request.EnableBuffering();

                    if (securityAttribute.EncodeInputs)
                    {
                        using var reader = new StreamReader(context.Request.Body);
                        var body = await reader.ReadToEndAsync();

                        if (!string.IsNullOrEmpty(body))
                        {
                            body = HtmlSanitizer.Sanitize(body, false, securityAttribute.IgnoreLinks);
                            //var jsonData = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);

                            //var sanitizedData = HtmlSanitizer.SanitizeJsonFields(jsonData, true, false, true);

                            //var sanitizedJson = JsonConvert.SerializeObject(sanitizedData);

                            var byteArray = System.Text.Encoding.UTF8.GetBytes(body); // sanitizedJson
                            context.Request.Body = new MemoryStream(byteArray);
                            context.Request.ContentLength = byteArray.Length;

                            context.Request.Body.Seek(0, SeekOrigin.Begin);
                        }
                    }
                    else
                    {
                        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                        var sanitizedBody = HtmlSanitizer.Sanitize(body, securityAttribute.EncodeInputs, securityAttribute.IgnoreLinks);

                        var requestBodyStream = new MemoryStream();
                        var writer = new StreamWriter(requestBodyStream);
                        await writer.WriteAsync(sanitizedBody);
                        await writer.FlushAsync();
                        requestBodyStream.Position = 0;
                        context.Request.Body = requestBodyStream;
                    }
                }
            }
            else if (!securityAttribute?.Disable ?? true)
            {
                context.Request.Path = HtmlSanitizer.Sanitize(context.Request.Path);

                foreach (var key in context.Request.Query.Keys)
                {
                    context.Request.Query = new QueryCollection(
                        context.Request.Query.ToDictionary(q => q.Key, q => (Microsoft.Extensions.Primitives.StringValues)HtmlSanitizer.Sanitize(q.Value))
                    );
                }

                //foreach (var key in context.Request.Headers.Keys)
                //{
                //    context.Request.Headers[key] = HtmlSanitizer.Sanitize(context.Request.Headers[key]);
                //}

                if (context.Request.Method == HttpMethods.Post || context.Request.Method == HttpMethods.Put)
                {
                    context.Request.EnableBuffering();

                    using var reader = new StreamReader(context.Request.Body);
                    var body = await reader.ReadToEndAsync();

                    if (!string.IsNullOrEmpty(body))
                    {
                        body = HtmlSanitizer.Sanitize(body);
                        //var jsonData = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);

                        //var sanitizedData = HtmlSanitizer.SanitizeJsonFields(jsonData, true, false, true);

                        //var sanitizedJson = JsonConvert.SerializeObject(sanitizedData);

                        var byteArray = System.Text.Encoding.UTF8.GetBytes(body); // sanitizedJson
                        context.Request.Body = new MemoryStream(byteArray);
                        context.Request.ContentLength = byteArray.Length;
                        context.Request.Body.Seek(0, SeekOrigin.Begin);
                    }
                }
            }

            await _next(context);
        }
    }
}
