using System.Net;
using Utilities.Enums;

namespace Utilities.Exceptions.Common
{
    public class BaseException : Exception
    {
        public HttpStatusCode HttpStatusCode { get; set; }
        public ApiResultStatusCode ApiStatusCode { get; set; }
        public object AdditionalData { get; set; }

        public BaseException()
            : this(ApiResultStatusCode.ServerError)
        {
        }

        public BaseException(ApiResultStatusCode statusCode)
            : this(statusCode, null)
        {
        }

        public BaseException(string message)
            : this(ApiResultStatusCode.ServerError, message)
        {
        }

        public BaseException(ApiResultStatusCode statusCode, string message)
            : this(statusCode, message, HttpStatusCode.InternalServerError)
        {

        } public BaseException(HttpStatusCode httpStatus ,ApiResultStatusCode statusCode, string message)
            : this(statusCode, message, httpStatus)
        {
        }

        public BaseException(string message, object additionalData)
            : this(ApiResultStatusCode.ServerError, message, additionalData)
        {
        }

        public BaseException(ApiResultStatusCode statusCode, object additionalData)
            : this(statusCode, null, additionalData)
        {
        }

        public BaseException(ApiResultStatusCode statusCode, string message, object additionalData)
            : this(statusCode, message, HttpStatusCode.InternalServerError, additionalData)
        {
        }

        public BaseException(ApiResultStatusCode statusCode, HttpStatusCode httpStatusCode, string message)
            : this(statusCode, message, httpStatusCode)//***
        {
        }

        public BaseException(ApiResultStatusCode statusCode, string message, HttpStatusCode httpStatusCode)
            : this(statusCode, message, httpStatusCode, null)
        {
        }

        public BaseException(ApiResultStatusCode statusCode, string message, HttpStatusCode httpStatusCode, object additionalData)
            : this(statusCode, message, httpStatusCode, null, additionalData)
        {
        }

        public BaseException(string message, Exception exception)
            : this(ApiResultStatusCode.ServerError, message, exception)
        {
        }

        public BaseException(string message, Exception exception, object additionalData)
            : this(ApiResultStatusCode.ServerError, message, exception, additionalData)
        {
        }

        public BaseException(ApiResultStatusCode statusCode, string message, Exception exception)
            : this(statusCode, message, HttpStatusCode.InternalServerError, exception)
        {
        }

        public BaseException(ApiResultStatusCode statusCode, string message, Exception exception, object additionalData)
            : this(statusCode, message, HttpStatusCode.InternalServerError, exception, additionalData)
        {
        }

        public BaseException(ApiResultStatusCode statusCode, string message, HttpStatusCode httpStatusCode, Exception exception)
            : this(statusCode, message, httpStatusCode, exception, null)
        {
        }

        public BaseException(ApiResultStatusCode statusCode, string message, HttpStatusCode httpStatusCode, Exception exception, object additionalData)
            : base(message, exception)
        {
            ApiStatusCode = statusCode;
            HttpStatusCode = httpStatusCode;
            AdditionalData = additionalData;
        }
    }
}
