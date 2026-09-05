using M1Mentor.Domain.Repositories.Contracts;
using System.Net;
using Utilities.Enums;
using Utilities.Extensions;
using Utilities.Filters;
using Utilities.Models.Storages;

namespace M1Mentor.Api.Utilities.Middlewares
{
    public class SecurityStampMiddleware(
        RequestDelegate _next,
        IUserRepository _userRepository,
        SecurityStampStorage securityStampStorage)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var authorizeAttribute = context.GetEndpoint()?.Metadata.GetMetadata<AuthorizeAttribute>();

            if (authorizeAttribute is null)
            {
                await _next(context);
                return;
            }

            var jwtToken = context.GetToken();
            var publicKey = jwtToken.GetClaim(Claims.PublicKey.ToDisplay())?.Value;
            var userType = jwtToken.GetClaim(Claims.UserType.ToDisplay())?.Value;
            var securityStamp = jwtToken.GetClaim(Claims.SecurityStamp.ToDisplay())?.Value;

            if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(securityStamp))
            {
                await _next(context);
                return;
            }

            var cachedStamp = securityStampStorage.GetSecurityStamp(userType, publicKey);
            if (cachedStamp != null)
            {
                if (cachedStamp != securityStamp)
                {
                    await context.WriteToResponseAsync("Authorization error", HttpStatusCode.Unauthorized,
                        ApiResultStatusCode.UnAuthorized);
                    return;
                }

                await _next(context);
                return;
            }

            string dbSecurityStamp = null;


                var user = await _userRepository.GetUserByPublicKeyAsync(publicKey);
                dbSecurityStamp = user?.SecurityStamp;

                if (dbSecurityStamp != null)
                {
                    securityStampStorage.UpdateSecurityStamp(userType, publicKey, dbSecurityStamp);

                    if (dbSecurityStamp != securityStamp)
                    {
                        await context.WriteToResponseAsync("Authorization error", HttpStatusCode.Unauthorized,
                            ApiResultStatusCode.UnAuthorized);
                        return;
                    }
                }

            await _next(context);
        }
    }
}