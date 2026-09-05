using Microsoft.AspNetCore.Http;
using Utilities.Constants;
using Utilities.Enums;
using Utilities.Extensions;
using Utilities.Services.Contracts;

namespace Utilities.Middlewares
{
    public class JwtMiddleware(RequestDelegate next, IJwtService jwtService)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var token = context.Request.Headers.Authorization.FirstOrDefault()?.Split(" ").Last();

            if (!string.IsNullOrEmpty(token))
            {
    
                if (!jwtService.TryValidate(token, out var jwtToken, out var tokenError))
                {
                    context.Items["TokenError"] = tokenError;
                    await next(context);
                    return;
                }


                context.Items["Token"] = jwtToken;

                var publicKey = jwtToken?.Claims.FirstOrDefault(c => c.Type == Claims.PublicKey.ToDisplay())?.Value ?? "system";
                var fullName = jwtToken?.Claims.FirstOrDefault(c => c.Type == Claims.FullName.ToDisplay())?.Value ?? "system";
                var role = jwtToken?.Claims.FirstOrDefault(c => c.Type == Claims.Role.ToDisplay())?.Value ?? "system";
                var type = jwtToken?.Claims.FirstOrDefault(c => c.Type == Claims.UserType.ToDisplay())?.Value ?? "system";
                var permissions = jwtToken?.Claims.Where(c => c.Type == Claims.Permission.ToDisplay())?.Select(c => c.Value).ToList() ?? [];

                CurrentRequestContext.User = new RequestUserInfo
                {
                    PublicKey = publicKey,
                    UserFullName = fullName,
                    Role = role,
                    Type = type,
                    Permissions = permissions
                };
            }

            await next(context);
        }
    }
}
