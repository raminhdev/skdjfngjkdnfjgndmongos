using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Utilities.Enums;
using Utilities.Exceptions.Common;
using Utilities.Extensions;

namespace Utilities.Middlewares
{
    public class CustomExceptionHandlerMiddleware(RequestDelegate next, IWebHostEnvironment env,
        ILogger<CustomExceptionHandlerMiddleware> logger)
    {
        public async Task Invoke(HttpContext context)
        {
            string message = null;
            HttpStatusCode httpStatusCode = HttpStatusCode.InternalServerError;
            ApiResultStatusCode apiStatusCode = ApiResultStatusCode.ServerError;

            try
            {
                await next(context);
            }
            catch (BaseException exception)
            {
                logger.LogError(exception, message: exception.Message);
                httpStatusCode = exception.HttpStatusCode;
                apiStatusCode = exception.ApiStatusCode;

                if (env.IsDevelopment())
                {
                    var dic = new Dictionary<string, string>
                    {
                        ["Exception"] = exception.Message,
                        ["StackTrace"] = exception.StackTrace,
                    };
                    if (exception.InnerException != null)
                    {
                        dic.Add("InnerException.Exception", exception.InnerException.Message);
                        dic.Add("InnerException.StackTrace", exception.InnerException.StackTrace);
                    }
                    if (exception.AdditionalData != null)
                        dic.Add("AdditionalData", JsonConvert.SerializeObject(exception.AdditionalData));

                    message = JsonConvert.SerializeObject(dic);
                }
                else
                {
                    message = exception.Message;
                }
                await context.WriteToResponseAsync(message, httpStatusCode, apiStatusCode);
            }
            catch (SecurityTokenExpiredException exception)
            {
                logger.LogError(exception, message: exception.Message);
                context.SetUnAuthorizeResponse(exception, env, ref httpStatusCode, ref apiStatusCode, ref message);
                await context.WriteToResponseAsync(message, httpStatusCode, apiStatusCode);
            }
            catch (UnauthorizedAccessException exception)
            {
                logger.LogError(exception, message: exception.Message);
                context.SetUnAuthorizeResponse(exception, env, ref httpStatusCode, ref apiStatusCode, ref message);
                await context.WriteToResponseAsync(message, httpStatusCode, apiStatusCode);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, message: exception.Message);

                if (env.IsDevelopment())
                {
                    var dic = new Dictionary<string, string>
                    {
                        ["Exception"] = exception.Message,
                        ["StackTrace"] = exception.StackTrace,
                    };
                    message = JsonConvert.SerializeObject(dic);
                }
                else
                {
                    message = "Something went wrong. please try again later";
                }

                await context.WriteToResponseAsync(message, httpStatusCode, apiStatusCode);
            }
        }
    }
}
