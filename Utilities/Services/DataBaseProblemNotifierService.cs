using System.Diagnostics;

namespace Utilities.Services
{
    public static class DataBaseProblemNotifierService
    {
        private static readonly string botToken = "7820603129:AAHycl6w62IlMFKw_8VlZZrnjnF61pfsBnA";
        private static readonly string chatId = "-1002867165963";

        public static async Task Notify(string? message)
        {
            message ??= "DataBase is down ⚠⚠";

            using HttpClient client = new();

            Stopwatch stopwatch = Stopwatch.StartNew();


            string url = $"https://api.telegram.org/bot{botToken}/sendMessage";
            var parameters = new Dictionary<string, string>
            {
                { "chat_id", chatId },
                { "text", message }
            };

            var response = await client.PostAsync(url, new FormUrlEncodedContent(parameters));

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Trying to send {message}");
                for (int i = 2; i > 1; i++)
                {
                    await client.PostAsync(url, new FormUrlEncodedContent(parameters));
                }
            }
            else
            {
                Console.WriteLine($"Sent: {message}");
            }

            await Task.Delay(1000);
        }
    }
}
