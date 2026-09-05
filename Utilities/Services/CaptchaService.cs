using System.Text.Json;
using Utilities.Constants;
using Utilities.Exceptions.Common;
using Utilities.Extensions;
using Utilities.Models.Results;
using Utilities.Services.Contracts;
using static Utilities.Constants.RegisterMode;

namespace Utilities.Services
{
    public class CaptchaService(CaptchaSettings _captchaSettings) : ICaptchaService, ISingletonDependency
    {
        public async Task ValidateCaptchaAsync(string captchaKey, string captchaCode)
        {
            try
            {
                using var client = new HttpClient();

                Dictionary<string, string> body = new()
                {
                    { "captcha_id", captchaKey},
                    { "user_input", captchaCode},
                };

                var result = await client.PostAsJsonAsync<JsonElement>($"{_captchaSettings.BaseUrl}/verify_captcha", body);

                bool isValid = result.GetProperty("success").GetBoolean();

                if (!isValid)
                    throw new BadRequestException(result.GetProperty("message").GetString());
            }
            catch (BadRequestException ex)
            {
                throw new BadRequestException(ex.Message);
            }
            catch (Exception ex)
            {
                throw new BaseException(ex.Message);
            }
        }

        public async Task<GetCaptchaResult> GetCaptchaAsync()
        {
            try
            {
                using var client = new HttpClient();

                var result = await client.GetAsJsonAsync<JsonElement>($"{_captchaSettings.BaseUrl}/generate_captcha");

                return new GetCaptchaResult()
                {
                    CaptchaKey = result.GetProperty("captcha_id").GetString(),
                    CaptchaImage = result.GetProperty("image_base64").GetString(),
                };
            }
            catch (Exception ex)
            {
                throw new BaseException(ex.Message);
            }
        }
    }
}
