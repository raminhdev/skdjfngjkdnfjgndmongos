using Utilities.Enums;
using Utilities.Exceptions.Common;

namespace Utilities.Exceptions
{
    public class DuplicateException : BaseException
    {
        public DuplicateException()
           : base(ApiResultStatusCode.Duplicated)
        {
        }
        public DuplicateException(string message)
            : base(ApiResultStatusCode.Duplicated, message)
        {
        }
    }
}
