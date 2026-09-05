using System.Text;
using Microsoft.AspNetCore.Http;
using Utilities.Attributes;
using Utilities.Exceptions.Common;
using Utilities.Models.Settings;
using Utilities.Services.Contracts;

namespace Utilities.Middlewares
{
    public class SignatureMiddleware(RequestDelegate _next, ISignatureService _signatureService,
        INonceService _nonceService, ApplicationPoolSettings _applicationPool)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var IgnoreSignatureAttribute = context.GetEndpoint()?.Metadata.GetMetadata<IgnoreSignatureAttribute>();
            if (IgnoreSignatureAttribute != null)
            {
                await _next(context);
                return;
            }

            var headers = context.Request.Headers;

            var applicationId = headers["ApplicationId"].FirstOrDefault();
            var nonce = headers["Nonce"].FirstOrDefault();
            var signature = headers["Signature"].FirstOrDefault();

            var application = _applicationPool.Applications
                .FirstOrDefault(q => q.ApplicationId == applicationId);


            if (application == null)
                throw new BadRequestException("Invalid Application");

            if (string.IsNullOrEmpty(signature))
                throw new BadRequestException("Signature is not specified");

            bool isMaster = signature == application.MasterSignature;

            if (!isMaster && string.IsNullOrEmpty(nonce))
                throw new BadRequestException("Nonce is not specified");


            if (!isMaster)
            {
                if (!_nonceService.TryUse(nonce, TimeSpan.FromMinutes(5)))
                    throw new BadRequestException("Duplicate nonce");

                byte[] signatureBytes = Convert.FromBase64String(signature);

                bool isValidSignature = _signatureService.Verify(
                    Encoding.UTF8.GetBytes(application.PreSharedKey),
                    Encoding.UTF8.GetBytes(nonce ?? string.Empty),
                    signatureBytes
                );

                if (!isValidSignature)
                    throw new BadRequestException("Signature is not valid");
            }

            await _next(context);
        }
    }
}



//public async Task InvokeAsync(HttpContext context)
//{
//    var applicationId = context.Request.Headers["ApplicationId"].FirstOrDefault();
//    var nonce = context.Request.Headers["Nonce"].FirstOrDefault();
//    var signature = context.Request.Headers["Signature"].FirstOrDefault();

//    var application = _applicationPool.Applications.FirstOrDefault(q => q.ApplicationId == applicationId);

//    if (application == null)
//        throw new BadRequestException("Invalid Application");
//    //{
//    //    await context.WriteToResponseAsync("Invalid Application", HttpStatusCode.BadRequest, ApiResultStatusCode.BadRequest);
//    //    return;
//    //}

//    if (string.IsNullOrEmpty(nonce) && signature != application.MasterSignature)
//        throw new BadRequestException("Nonce is not specified");
//    //{
//    //    await context.WriteToResponseAsync("Nonce is not specified", HttpStatusCode.BadRequest, ApiResultStatusCode.BadRequest);
//    //    return;
//    //}

//    if (string.IsNullOrEmpty(signature))
//        throw new BadRequestException("Signature is not specified");
//    //{
//    //    await context.WriteToResponseAsync("Signature is not specified", HttpStatusCode.BadRequest, ApiResultStatusCode.BadRequest);
//    //    return;
//    //}

//    if (_nonceService.Contains(nonce) && signature != application.MasterSignature)
//        throw new BadRequestException("Duplicate nonce");
//    //{
//    //    await context.WriteToResponseAsync("Duplicate nonce", HttpStatusCode.BadRequest, ApiResultStatusCode.BadRequest);
//    //    return;
//    //}

//    if (signature != application.MasterSignature)
//    {
//        byte[] signatureBytes = Convert.FromBase64String(signature);

//        bool verification = _signatureService.Verify(Encoding.UTF8.GetBytes(application.PreSharedKey),
//                           Encoding.UTF8.GetBytes(nonce ?? string.Empty), signatureBytes);

//        if (!verification)
//            throw new BadRequestException("Signature is not valid");
//        //{
//        //    //await context.WriteToResponseAsync("Signature is not valid", HttpStatusCode.BadRequest, ApiResultStatusCode.BadRequest);
//        //    //return;
//        //}

//        _nonceService.Add(nonce);
//    }

//    await _next(context);
//}