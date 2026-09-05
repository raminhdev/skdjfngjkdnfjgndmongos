using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Utilities.Enums;
using Utilities.Extensions;

namespace Utilities.Api
{
    public class ApiBaseController : ControllerBase
    {
        //protected virtual string JwtTokenAsString => HttpContext.Items["Token"].ToString();
        protected virtual JwtSecurityToken JwtToken => (JwtSecurityToken)HttpContext.Items["Token"];
        protected virtual string PublicKey => HttpContext.GetClaim(Claims.PublicKey.ToDisplay());
        protected virtual string Language => HttpContext.Request.Headers["Accept-Language"];
        protected virtual string Ip => HttpContext.GetRequestIpv4();

        protected virtual string Nonce => HttpContext.Request.Headers["DecryptedNonce"].ToString();
        protected virtual string WalletAddress => HttpContext.GetClaim(Claims.WalletAddress.ToDisplay());   
    }
}
