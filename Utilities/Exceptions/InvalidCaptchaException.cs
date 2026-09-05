using Utilities.Enums;
using Utilities.Exceptions.Common;

namespace M1Mentor.Utilities.Exceptions
{
    public class InvalidCaptchaException : BaseException
    {
        public InvalidCaptchaException()
           : base(ApiResultStatusCode.InvalidCaptcha)
        {
        }
        public InvalidCaptchaException(string message)
            : base(ApiResultStatusCode.InvalidCaptcha, message)
        {
        }

    }
}
