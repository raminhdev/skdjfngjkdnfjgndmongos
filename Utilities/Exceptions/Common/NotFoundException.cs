using System.Net;
using Utilities.Enums;

namespace Utilities.Exceptions.Common
{
    public class NotFoundException : BaseException
    {
        public NotFoundException()
            : base(ApiResultStatusCode.NotFound)
        {
        }

        public NotFoundException(string message)
            : base(ApiResultStatusCode.NotFound, message)
        {
        }


        public NotFoundException(ApiResultStatusCode status, string message)
          : base(HttpStatusCode.NotFound, status, message)
        {
        }

        public NotFoundException(object additionalData, string message)
            : base(ApiResultStatusCode.NotFound, additionalData)
        {
        }

        public NotFoundException(string message, object additionalData)
            : base(ApiResultStatusCode.NotFound, message, additionalData)
        {
        }

        public NotFoundException(string message, Exception exception)
            : base(ApiResultStatusCode.NotFound, message, exception)
        {
        }

        public NotFoundException(string message, Exception exception, object additionalData)
            : base(ApiResultStatusCode.NotFound, message, exception, additionalData)
        {
        }
    }
}
