using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Utilities.Extensions
{
    public static class JwtSecurityTokenExtensions
    {
        public static Claim GetClaim(this JwtSecurityToken token, string type)
        {
            return token?.Claims?.SingleOrDefault(q => q.Type == type);
        }

        public static IList<Claim> GetClaims(this JwtSecurityToken token, string type)
        {
            return token.Claims.Where(q => q.Type == type).ToList();
        }

        public static bool HasClaim(this JwtSecurityToken token, string type, string value)
        {
            return token.GetClaims(type).Any(q => q.Value == value);
        }
    }
}
