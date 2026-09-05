using System.Net;
using Utilities.Enums;
using Utilities.Exceptions.Common;

namespace Utilities.Exceptions
{
    public class TooManyRequestsException : BaseException
    {
        public TooManyRequestsException()
           : base(ApiResultStatusCode.TooManyRequests)
        {
        }

        public TooManyRequestsException(string message)
            : base(ApiResultStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests, message)
        {
        }
    }
}
