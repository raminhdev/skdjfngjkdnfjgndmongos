using Utilities.Enums;
using Utilities.Exceptions.Common;

namespace Utilities.Exceptions
{
    public class OAuthException : BaseException
    {
        public OAuthException()
           : base(ApiResultStatusCode.OAuth)
        {
        }
        public OAuthException(string message)
            : base(ApiResultStatusCode.OAuth, message)
        {
        }
    }
}
