using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Utilities.Enums;

namespace Utilities.Services.Contracts
{
    public interface IJwtService
    {
        AccessToken Generate(IEnumerable<Claim> claims);

        JwtSecurityToken Validate(string token);

        /// <summary>
        /// Normal-auth validation that also reports WHY a token failed (expired / bad signature /
        /// bad issuer / bad audience / missing / malformed) so the caller can return a precise message.
        /// </summary>
        bool TryValidate(string token, out JwtSecurityToken jwtToken, out TokenValidationError error);

        /// <summary>
        /// Validates signature/issuer/audience/decryption of a token while ignoring its lifetime.
        /// Used by the refresh-token flow so an access token that has already expired can still be
        /// presented to obtain a new token pair (the refresh token — not the access token lifetime —
        /// is the authority for renewal).
        /// </summary>
        // JwtSecurityToken ValidateIgnoringLifetime(string token);
        

        JwtSecurityToken ValidateExpiredToken(string token);

        /// <summary>
        /// Refresh-flow only: returns the principal of a possibly-expired access token. Validates
        /// issuer, audience, signing key and signature; only the lifetime check is disabled. Must
        /// never be used on the normal authentication path (protected endpoints keep rejecting
        /// expired tokens).
        /// </summary>
        ClaimsPrincipal GetPrincipalFromExpiredToken(string token);

        ActionResult Authenticate(string publicKey, IEnumerable<string> permissions,
            UserType userType, string securityStamp, string role, string fullName);

        string GenerateRefreshToken();
    }
}