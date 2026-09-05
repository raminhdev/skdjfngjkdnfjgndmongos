using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Utilities.Exceptions;
using Utilities.Extensions;

namespace Utilities.Middlewares
{
    public class CustomGlobalRateLimitingMiddleware(RequestDelegate next, IMemoryCache cache)
    {
        public async Task Invoke(HttpContext context)
        {
            var identifier = context.GetClaim("Publickey") ?? context.GetRequestIpv4().Split(",")[0];
            var cacheKey = $"{identifier}";

            if (cache.TryGetValue(cacheKey, out int attempts))
            {
                if (attempts >= 60)
                {
                    throw new TooManyRequestsException("Too many requests. Try again after 1 minute");
                    //await context.WriteToResponseAsync("Too many requests. Try again after 1 minute", HttpStatusCode.TooManyRequests, ApiResultStatusCode.TooManyRequests);
                    //return;
                }
            }

            attempts = cache.TryGetValue(cacheKey, out int existingAttempts) ? existingAttempts + 1 : 1;
            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) // 60 Request in 1 min
            };
            cache.Set(cacheKey, attempts, cacheEntryOptions);

            await next(context);
        }
    }
}