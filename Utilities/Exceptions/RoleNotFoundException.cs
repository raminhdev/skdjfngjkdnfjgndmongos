using Utilities.Enums;
using Utilities.Exceptions.Common;

namespace Utilities.Exceptions
{
    public class RoleNotFoundException : BaseException
    {
        public RoleNotFoundException()
            : base(ApiResultStatusCode.RoleNotFound)
        {
        }
        public RoleNotFoundException(string message)
            : base(ApiResultStatusCode.RoleNotFound, System.Net.HttpStatusCode.NotFound, message)
        {
        }

    }
}
