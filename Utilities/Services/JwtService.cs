using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Utilities.Constants;
using Utilities.Enums;
using Utilities.Exceptions.Common;
using Utilities.Extensions;
using Utilities.Services.Contracts;

namespace Utilities.Services
{
    public class JwtService(JwtServiceSettings _settings)
        : IJwtService, RegisterMode.ISingletonDependency
    {
        public AccessToken Generate(IEnumerable<Claim> claims)
        {
            var signatureKey = Encoding.UTF8.GetBytes(_settings.SignatureKey);
            var signingCredentials = new SigningCredentials(new SymmetricSecurityKey(signatureKey),
                SecurityAlgorithms.HmacSha256Signature);

            var encryptionKey = Encoding.UTF8.GetBytes(_settings.EncryptionKey);
            var encryptingCredentials = new EncryptingCredentials(new SymmetricSecurityKey(encryptionKey),
                SecurityAlgorithms.Aes128KW, SecurityAlgorithms.Aes128CbcHmacSha256);

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _settings.Issuer,
                Audience = _settings.Audience,
                IssuedAt = DateTime.UtcNow,
                NotBefore = DateTime.UtcNow,
                Expires = DateTime.UtcNow.AddHours(_settings.AccessTokenExpiresAfterHours),
                SigningCredentials = signingCredentials,
                EncryptingCredentials = encryptingCredentials,
                Subject = new ClaimsIdentity(claims),
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.CreateJwtSecurityToken(descriptor);

            return new AccessToken(securityToken, GenerateRefreshToken());
        }

        // Normal API authentication path: the token lifetime IS enforced, so expired access tokens
        // are rejected on protected endpoints.
        public JwtSecurityToken Validate(string token) => ValidateInternal(token, validateLifetime: true).token;

        /// <summary>
        /// Normal-auth validation that classifies WHY a token failed (expired vs bad signature vs bad
        /// issuer/audience vs malformed) so callers can return an honest, specific error message.
        /// Lifetime IS enforced — expired tokens are reported as <see cref="TokenValidationError.Expired"/>.
        /// </summary>
        public bool TryValidate(string token, out JwtSecurityToken jwtToken, out TokenValidationError error)
        {
            jwtToken = null;
            error = TokenValidationError.None;

            if (string.IsNullOrWhiteSpace(token))
            {
                error = TokenValidationError.Missing;
                return false;
            }

            try
            {
                jwtToken = ValidateInternal(token, validateLifetime: true).token;
                return true;
            }
            catch (SecurityTokenExpiredException)
            {
                error = TokenValidationError.Expired;
            }
            catch (SecurityTokenInvalidSignatureException)
            {
                error = TokenValidationError.InvalidSignature;
            }
            catch (SecurityTokenInvalidIssuerException)
            {
                error = TokenValidationError.InvalidIssuer;
            }
            catch (SecurityTokenInvalidAudienceException)
            {
                error = TokenValidationError.InvalidAudience;
            }
            catch (Exception)
            {
                error = TokenValidationError.Invalid;
            }

            return false;
        }

        // Refresh-only path: same cryptographic + issuer/audience checks, but lifetime is NOT
        // enforced so an already-expired access token can still be parsed for its claims.
        // public JwtSecurityToken ValidateIgnoringLifetime(string token) => ValidateInternal(token, validateLifetime: false).token;

        /// <summary>
        /// Refresh-flow only: extracts the <see cref="ClaimsPrincipal"/> from a possibly-expired
        /// access token. Issuer, audience, signing key and signature are all validated — ONLY the
        /// lifetime check is disabled. Never use this on the normal authentication path.
        /// </summary>
        public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
            => ValidateInternal(token, validateLifetime: false).principal;

        private (ClaimsPrincipal principal, JwtSecurityToken token) ValidateInternal(string token, bool validateLifetime)
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            var signatureKey = Encoding.UTF8.GetBytes(_settings.SignatureKey);
            var signingCredentials = new SigningCredentials(new SymmetricSecurityKey(signatureKey),
                SecurityAlgorithms.HmacSha256Signature);

            var encryptionKey = Encoding.UTF8.GetBytes(_settings.EncryptionKey);
            var encryptingCredentials = new EncryptingCredentials(new SymmetricSecurityKey(encryptionKey),
                SecurityAlgorithms.Aes128KW, SecurityAlgorithms.Aes128CbcHmacSha256);

            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,            // signing key must match
                IssuerSigningKey = signingCredentials.Key,
                TokenDecryptionKey = encryptingCredentials.Key,
                ValidateIssuer = true,                      // issuer must match
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,                    // audience must match
                ValidAudience = _settings.Audience,
                ValidateLifetime = validateLifetime,        // ONLY this is relaxed for refresh
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            return (principal, (JwtSecurityToken)validatedToken);
        }

        public ActionResult Authenticate(string publicKey, IEnumerable<string> permissions,
            UserType userType, string securityStamp, string role, string fullName)
            => new JsonResult(Generate(GetClaims(publicKey, permissions, userType, securityStamp, role, fullName)));
        
        public JwtSecurityToken ValidateExpiredToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            var signatureKey = Encoding.UTF8.GetBytes(_settings.SignatureKey);

            var encryptionKey = Encoding.UTF8.GetBytes(_settings.EncryptionKey);

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(signatureKey),

                TokenDecryptionKey = new SymmetricSecurityKey(encryptionKey),

                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,

                ValidateAudience = true,
                ValidAudience = _settings.Audience,

                ValidateLifetime = false,

                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            return (JwtSecurityToken)validatedToken;
        }


        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        #region Private Methods

        private static List<Claim> GetClaims(string publicKey, IEnumerable<string> permissions,
            UserType userType, string securityStamp, string role, string fullName)
        {
            try
            {
                var claims = new List<Claim>
                {
                    new(Claims.PublicKey.ToDisplay(), publicKey),
                    new(Claims.UserType.ToDisplay(), userType.ToDisplay()),
                    new(Claims.SecurityStamp.ToDisplay(), securityStamp),
                    new(Claims.Role.ToDisplay(), role),
                    new(Claims.FullName.ToDisplay(), fullName),
                };

                if (permissions != null)
                {
                    claims.AddRange(permissions.Select(permission =>
                        new Claim(Claims.Permission.ToDisplay(), permission)));
                }

                return claims;
            }
            catch (Exception ex)
            {
                throw new BaseException(ex.Message);
            }
        }

        #endregion
    }

    public class AccessToken(JwtSecurityToken securityToken, string refreshToken)
    {
        public string access_token { get; set; } = new JwtSecurityTokenHandler().WriteToken(securityToken);
        public string refresh_token { get; set; } = refreshToken;
        public string token_type { get; set; } = "Bearer";
        public int expires_in { get; set; } = (int)(securityToken.ValidTo - DateTime.UtcNow).TotalSeconds;
    }

    public class TokenRequest
    {
        [Required] public string grant_type { get; set; }
        [Required] public string username { get; set; }
        [Required] public string password { get; set; }
        public string refresh_token { get; set; }
        public string scope { get; set; }
        public string client_id { get; set; }
        public string client_secret { get; set; }
    }


    public class RefreshTokenRequest
    {
        [Required] public string grant_type { get; set; } = "refresh_token";
        [Required] public string access_token { get; set; }
        [Required] public string refresh_token { get; set; }
        public string client_id { get; set; }
        public string client_secret { get; set; }
    }
}