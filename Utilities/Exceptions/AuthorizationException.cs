using Utilities.Enums;
using Utilities.Exceptions.Common;

namespace Utilities.Exceptions
{
    public class AuthorizationException : BaseException
    {
        public AuthorizationException()
           : base(ApiResultStatusCode.UnAuthorized)
        {
        }
        public AuthorizationException(string message)
            : base(ApiResultStatusCode.UnAuthorized, System.Net.HttpStatusCode.Unauthorized, message)
        {
        }
    }
}
