using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Utilities.Filters
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class FileSizeLimitAttribute(long MaxFileSize) : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var files = context.HttpContext.Request.Form.Files;

            foreach (var file in files)
            {
                if (file.Length > MaxFileSize)
                {
                    context.ModelState.AddModelError("File", $"File size cannot exceed {MaxFileSize / (1024 * 1024)}MB");
                    context.HttpContext.Response.StatusCode = 400;
                    context.Result = new BadRequestObjectResult(context.ModelState);
                    return;
                }
            }

            base.OnActionExecuting(context);
        }
    }
}
