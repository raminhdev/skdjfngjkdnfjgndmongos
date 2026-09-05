using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Caching.Memory;
using Utilities.Attributes;
using Utilities.Enums;
using Utilities.Exceptions;
using Utilities.Extensions;

namespace Utilities.Middlewares
{
    public class CustomRateLimitingMiddleware(RequestDelegate next, IMemoryCache cache)
    {
        private static readonly ConcurrentDictionary<string, object> _locks = new();

        public async Task Invoke(HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            var rateLimitAttr = endpoint?.Metadata.GetMetadata<CustomRateLimitAttribute>();

            if (rateLimitAttr == null)
            {
                await next(context);
                return;
            }

            // Get endpoint and identifier info
            var actionDescriptor = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();
            var endpointName = $"{actionDescriptor?.ControllerName ?? "Unknown"}.{actionDescriptor?.ActionName ?? "Unknown"}";
            var identifier = context.GetClaim(nameof(Claims.PublicKey)) ?? context.GetRequestIpv4() ?? "unknown";

            var requestListKey = $"ratelimit_{identifier}_{endpointName}_timestamps";
            var lockKey = $"ratelimit_{identifier}_{endpointName}_lock";

            if (cache.TryGetValue(lockKey, out _))
            {
                context.Response.Headers.RetryAfter = (rateLimitAttr.LockoutDurationMinutes * 60).ToString();

                await context.WriteToResponseAsync(rateLimitAttr.Message, HttpStatusCode.TooManyRequests, ApiResultStatusCode.TooManyRequests);
                return;
            }

            var lockObject = _locks.GetOrAdd(requestListKey, k => new object());

            lock (lockObject)
            {
                var now = DateTime.UtcNow;
                var timestamps = cache.GetOrCreate(requestListKey, entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(rateLimitAttr.PeriodSeconds);
                    return new ConcurrentQueue<DateTime>();
                });

                // Remove old timestamps (thread-safe within the lock)
                while (timestamps.TryPeek(out var oldest) && oldest < now.AddSeconds(-rateLimitAttr.PeriodSeconds))
                {
                    timestamps.TryDequeue(out _);
                }

                if (timestamps.Count >= rateLimitAttr.MaxAttemptsCount)
                {
                    cache.Set(lockKey, true, TimeSpan.FromMinutes(rateLimitAttr.LockoutDurationMinutes));
                    cache.Remove(requestListKey);

                    context.Response.Headers.RetryAfter = (rateLimitAttr.LockoutDurationMinutes * 60).ToString();
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                }
                else
                {
                    timestamps.Enqueue(now);

                    var resetTime = new DateTimeOffset(now.AddSeconds(rateLimitAttr.PeriodSeconds)).ToUnixTimeSeconds();
                    context.Response.Headers["X-RateLimit-Limit"] = rateLimitAttr.MaxAttemptsCount.ToString();
                    context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, rateLimitAttr.MaxAttemptsCount - timestamps.Count).ToString();
                    context.Response.Headers["X-RateLimit-Reset"] = resetTime.ToString();
                }
            }

            if (context.Response.StatusCode == StatusCodes.Status429TooManyRequests)
            {
                throw new TooManyRequestsException(rateLimitAttr.Message);
                //await context.WriteToResponseAsync(rateLimitAttr.Message, HttpStatusCode.TooManyRequests, ApiResultStatusCode.TooManyRequests);
                //return;
            }

            await next(context);
        }
    }
}
