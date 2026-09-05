using Utilities.Models.Results;

namespace Utilities.Services.Contracts
{
    public interface ICaptchaService
    {
        Task ValidateCaptchaAsync(string captchaKey, string captchaCode);
        Task<GetCaptchaResult> GetCaptchaAsync();
    }
}
