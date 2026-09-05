using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Utilities.Extensions
{
    public static class HttpClientExtensions
    {
        public static async Task<T> ReadAsJsonAsync<T>(this HttpResponseMessage response)
        {
            var options = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new JsonStringEnumConverter());
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<T>(json, options);
            return result;
        }

        public static async Task<T> GetAsJsonAsync<T>(this HttpClient httpClient, Uri uri,
            Dictionary<string, string> headers = null, AuthenticationHeaderValue authenticationHeader = null)
            => await httpClient.GetAsJsonAsync<T>(uri.AbsoluteUri, headers, authenticationHeader);

        public static async Task<T> PostAsJsonAsync<T>(this HttpClient httpClient, Uri uri, object content = null,
            Dictionary<string, string> headers = null, AuthenticationHeaderValue authenticationHeader = null)
            => await httpClient.PostAsJsonAsync<T>(uri.AbsoluteUri, content, headers, authenticationHeader);

        public static async Task<T> PutAsJsonAsync<T>(this HttpClient httpClient, Uri uri, object content = null,
            Dictionary<string, string> headers = null, AuthenticationHeaderValue authenticationHeader = null)
            => await httpClient.PutAsJsonAsync<T>(uri.AbsoluteUri, content, headers, authenticationHeader);

        public static async Task<T> DeleteAsJsonAsync<T>(this HttpClient httpClient, Uri uri,
            Dictionary<string, string> headers = null, AuthenticationHeaderValue authenticationHeader = null)
            => await httpClient.DeleteAsJsonAsync<T>(uri.AbsoluteUri, headers, authenticationHeader);

        public static async Task<T> GetAsJsonAsync<T>(this HttpClient httpClient, string url,
            Dictionary<string, string> headers = null, AuthenticationHeaderValue authenticationHeader = null)
        {
            using (var message = new HttpRequestMessage(HttpMethod.Get, url))
            {
                message.Headers.Authorization = authenticationHeader;

                foreach (KeyValuePair<string, string> header in headers ?? new Dictionary<string, string>())
                    message.Headers.Add(header.Key, header.Value);

                var response = await httpClient.SendAsync(message);

                return await response.ReadAsJsonAsync<T>();
            }
        }

        public static async Task<byte[]> GetAsFile(this HttpClient httpClient, string url,
            Dictionary<string, string> headers = null, AuthenticationHeaderValue authenticationHeader = null)
        {
            using (var message = new HttpRequestMessage(HttpMethod.Get, url))
            {
                message.Headers.Authorization = authenticationHeader;

                foreach (KeyValuePair<string, string> header in headers ?? new Dictionary<string, string>())
                    message.Headers.Add(header.Key, header.Value);

                var response = await httpClient.SendAsync(message);

                return await response.Content.ReadAsByteArrayAsync();
            }
        }

        public static async Task<T> PostAsJsonAsync<T>(this HttpClient httpClient, string url, object content = null,
            Dictionary<string, string> headers = null, AuthenticationHeaderValue authenticationHeader = null)
        {
            using (var message = new HttpRequestMessage(HttpMethod.Post, url))
            {
                message.Headers.Authorization = authenticationHeader;

                foreach (KeyValuePair<string, string> header in headers ?? new Dictionary<string, string>())
                    message.Headers.Add(header.Key, header.Value);

                message.Content =
                    new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json");

                var response = await httpClient.SendAsync(message);

                return await response.ReadAsJsonAsync<T>();
            }
        }

        public static async Task<byte[]> PostAsFile(this HttpClient httpClient, string url, object content = null,
            Dictionary<string, string> headers = null, AuthenticationHeaderValue authenticationHeader = null)
        {
            using (var message = new HttpRequestMessage(HttpMethod.Post, url))
            {
                message.Headers.Authorization = authenticationHeader;

                foreach (KeyValuePair<string, string> header in headers ?? new Dictionary<string, string>())
                    message.Headers.Add(header.Key, header.Value);

                message.Content =
                    new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json");

                var response = await httpClient.SendAsync(message);

                return await response.Content.ReadAsByteArrayAsync();
            }
        }

        public static async Task<T> PutAsJsonAsync<T>(this HttpClient httpClient, string url, object content = null,
            Dictionary<string, string> headers = null, AuthenticationHeaderValue authenticationHeader = null)
        {
            using (var message = new HttpRequestMessage(HttpMethod.Put, url))
            {
                message.Headers.Authorization = authenticationHeader;

                foreach (KeyValuePair<string, string> header in headers ?? new Dictionary<string, string>())
                    message.Headers.Add(header.Key, header.Value);

                message.Content =
                    new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json");

                var response = await httpClient.SendAsync(message);

                return await response.ReadAsJsonAsync<T>();
            }
        }

        public static async Task<T> DeleteAsJsonAsync<T>(this HttpClient httpClient, string url,
            Dictionary<string, string> headers = null, AuthenticationHeaderValue authenticationHeader = null)
        {
            using (var message = new HttpRequestMessage(HttpMethod.Delete, url))
            {
                message.Headers.Authorization = authenticationHeader;

                foreach (KeyValuePair<string, string> header in headers ?? new Dictionary<string, string>())
                    message.Headers.Add(header.Key, header.Value);

                var response = await httpClient.SendAsync(message);

                return await response.ReadAsJsonAsync<T>();
            }
        }
    }
}