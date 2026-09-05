using System.Net;
using Utilities.Enums;
using Utilities.Exceptions.Common;

namespace Utilities.Exceptions
{
    public class ConflictException : BaseException
    {
        public ConflictException()
            : base(ApiResultStatusCode.BadRequest)
        {
        }
        public ConflictException(ApiResultStatusCode status, string message)
         : base(HttpStatusCode.Conflict, status, message)
        {
        }
        public ConflictException(string message)
            : base(ApiResultStatusCode.Conflict, HttpStatusCode.Conflict, message)
        {
        }

        public ConflictException(object additionalData)
            : base(ApiResultStatusCode.Conflict, additionalData)
        {
        }

        public ConflictException(string message, object additionalData)
            : base(ApiResultStatusCode.Conflict, message, additionalData)
        {
        }

        public ConflictException(string message, Exception exception)
            : base(ApiResultStatusCode.Conflict, message, exception)
        {
        }

        public ConflictException(string message, Exception exception, object additionalData)
            : base(ApiResultStatusCode.Conflict, message, exception, additionalData)
        {
        }
    }
}
