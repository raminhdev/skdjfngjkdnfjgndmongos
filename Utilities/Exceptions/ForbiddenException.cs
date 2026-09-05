using System.Net;
using Utilities.Enums;
using Utilities.Exceptions.Common;

namespace Utilities.Exceptions
{
    public class ForbiddenException : BaseException
    {
        public ForbiddenException()
            : base(ApiResultStatusCode.Forbidden)
        {
        }

        public ForbiddenException(string message)
            : base(ApiResultStatusCode.Forbidden, HttpStatusCode.Forbidden, message)
        {
        }
    }
}
