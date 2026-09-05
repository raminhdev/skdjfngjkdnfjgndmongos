using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Utilities.Enums;
using Utilities.Exceptions;
using Utilities.Extensions;

namespace Utilities.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class AuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string[] _claims;

        public AuthorizeAttribute()
        {
        }

        public AuthorizeAttribute(params string[] claims)
        {
            _claims = claims;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var jwtSecurityToken = context.HttpContext.GetToken();

            if (jwtSecurityToken == null)
                throw new AuthorizationException(ResolveTokenErrorMessage(context.HttpContext));

            //if (_claims != null && !_claims.Any(c => jwtSecurityToken.HasClaim(Claims.Permission.ToDisplay(), c)))
            //    throw new BaseException(ApiResultStatusCode.Forbidden, "Access denied");

            if (_claims != null && !_claims.Any(c => jwtSecurityToken.HasClaim(Claims.Permission.ToDisplay(), c)))
                throw new ForbiddenException("Access denied");
        }

        // Translate the validation outcome recorded by JwtMiddleware into a precise, honest message.
        // A missing Authorization header is reported as "required"; a present-but-rejected token is
        // reported by its actual cause (expired vs invalid) — never a generic "unauthorized".
        private static string ResolveTokenErrorMessage(HttpContext httpContext)
        {
            var hasAuthorizationHeader =
                !string.IsNullOrWhiteSpace(httpContext.Request.Headers.Authorization.FirstOrDefault());

            if (!hasAuthorizationHeader)
                return "Authorization error";

            var error = httpContext.Items["TokenError"] as TokenValidationError?
                        ?? TokenValidationError.Invalid;

            return error switch
            {
                TokenValidationError.Missing => "Authorization error",
                TokenValidationError.Expired => "Access token has expired.",
                _ => "Invalid access token."
            };
        }
    }
}
