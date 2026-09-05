using System.Net;
using Utilities.Enums;

namespace Utilities.Exceptions.Common
{
    public class BadRequestException : BaseException
    {
        public BadRequestException()
            : base(ApiResultStatusCode.BadRequest)
        {
        }
        public BadRequestException(ApiResultStatusCode status, string message)
         : base(HttpStatusCode.BadRequest, status, message)
        {
        }
        public BadRequestException(string message)
            : base(ApiResultStatusCode.BadRequest, HttpStatusCode.BadRequest, message)
        {
        }

        public BadRequestException(object additionalData)
            : base(ApiResultStatusCode.BadRequest, additionalData)
        {
        }

        public BadRequestException(string message, object additionalData)
            : base(ApiResultStatusCode.BadRequest, message, additionalData)
        {
        }

        public BadRequestException(string message, Exception exception)
            : base(ApiResultStatusCode.BadRequest, message, exception)
        {
        }

        public BadRequestException(string message, Exception exception, object additionalData)
            : base(ApiResultStatusCode.BadRequest, message, exception, additionalData)
        {
        }
    }
}
