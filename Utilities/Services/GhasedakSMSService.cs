using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Utilities.Exceptions.Common;
using Utilities.Services.Contracts;
using static Utilities.Constants.RegisterMode;

namespace Utilities.Services
{
    public class GhasedakSMSService(GhasedakConnectionSetting _settings)
        : ISmsService, ISingletonDependency
    {
        private readonly HttpClient _httpClient = new()
        {
            BaseAddress = new Uri("https://api.ghasedaksms.com")
        };

        public async Task SendMessageAsync(string mobileNumber, string templateName, params string[] data)
        {
            try
            {
                var formData = new List<KeyValuePair<string, string>>
                {
                    new("receptor", mobileNumber),
                    new("type", "1"), // 1 = text, 2 = voice
                    new("template", templateName)
                };

                for (int i = 0; i < data.Length; i++)
                {
                    formData.Add(new($"param{i + 1}", data[i]));
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, "/v2/send/verify")
                {
                    Content = new FormUrlEncodedContent(formData)
                };

                request.Headers.Add("apikey", _settings.ApiKey);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode(); 

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(content);

                //result.TryGetProperty("messageids" out var statusProp);
                //result.TryGetProperty("result", out var resultProp);

                if (!result.TryGetProperty("messageids", out var statusProp) || !result.TryGetProperty("result", out var resultProp))
                {
                    throw new BaseException("خطایی هنگام ارسال پیامک رخ داد", $"Unexpected response shape: {content}");
                }

                var statusCode = statusProp.GetDouble();
                var message = resultProp.GetString();

                if (statusCode < 1000)
                {
                    throw new BaseException("خطایی هنگام ارسال پیامک رخ داد", $"SMS failed [{statusCode}]");
                }

            }
            catch (Exception ex)
            {
                throw new BaseException("خطایی هنگام ارسال پیامک رخ داد", ex.Message);
            }
        }

        public async Task SendTextMessageAsync(string mobileNumber, string message)
        {
            var body = new
            {
                receptor = mobileNumber,
                message
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "/v2/sms/send/simple")
            {
                Content = JsonContent.Create(body)
            };

            request.Headers.Add("apikey", _settings.ApiKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"SendTextMessage response: {content}");
        }

        public async Task SendVerificationMessageAsync(string mobileNumber, string code)
        {
            await SendMessageAsync(mobileNumber, _settings.VerificationTemplateName, code);
        }

        public async Task SendOneTimePasswordAsync(string mobileNumber, string password)
        {
            await SendMessageAsync(mobileNumber, _settings.ForgetPasswordTemplateName, password);
        }

        public async Task SendPlanCompletedMessage(string mobileNumber)
        {

            var pc = new PersianCalendar();
            var now = DateTime.Now;

            var date = $"{pc.GetYear(now)}.{pc.GetMonth(now):00}.{pc.GetDayOfMonth(now):00}";

            await SendMessageAsync(
                mobileNumber,
                _settings.PlanCompletedNotification,
                date);

        }

        public async Task SendPlanRequestMessage()
        {
            var pc = new PersianCalendar();
            var now = DateTime.Now;

            var date = $"{pc.GetYear(now)}.{pc.GetMonth(now):00}.{pc.GetDayOfMonth(now):00}";

            var adminNumbers = _settings.NotificationPhoneNumbers;

            foreach (var number in adminNumbers)
            {
                await SendMessageAsync(number, _settings.NewPlanRequestNotification, date);
            }

        }

        public async Task SendTicketNotificationMessage()
        {
            var pc = new PersianCalendar();
            var now = DateTime.Now;

            var date = $"{pc.GetYear(now)}.{pc.GetMonth(now):00}.{pc.GetDayOfMonth(now):00}";

            var adminNumbers = _settings.NotificationPhoneNumbers;

            foreach (var number in adminNumbers)
            {
                await SendMessageAsync(number, _settings.NewTicketNotification, date);
            }
        }
    }

    public class GhasedakConnectionSetting : IHostedDependency
    {
        public string ApiKey { get; set; }
        public string VerificationTemplateName { get; set; }
        public string ForgetPasswordTemplateName { get; set; }
        public string PlanCompletedNotification { get; set; }
        public string NewPlanRequestNotification { get; set; }
        public string NewTicketNotification { get; set; }
        public List<string> NotificationPhoneNumbers { get; set; }
    }
}
//برای ارسال پیام متنی type=1 و برای ارسال پیام صوتی type=2 قرار دهید
