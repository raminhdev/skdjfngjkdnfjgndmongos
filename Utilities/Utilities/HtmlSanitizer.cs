using System.Text.RegularExpressions;
using System.Web;

namespace Utilities.Utilities
{
    public static class HtmlSanitizer
    {
        private static readonly string[] BlackListTags = { "script", "iframe", "object", "embed", "form", "link", "style" };
        private static readonly string[] BlackListAttributes = { "onload", "onclick", "onerror", "onmouseover", "onblur", "href", "src", "style" };

        public static string Sanitize(string input, bool encodeInput = false, bool ignoreLinks = false, bool onlyEncode = false)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            if (onlyEncode)
                return HttpUtility.HtmlEncode(input).Trim();

            foreach (var tag in BlackListTags)
            {
                var tagRegex = new Regex($"<\\/?\\s*{tag}\\s*[^>]*>", RegexOptions.IgnoreCase);
                input = tagRegex.Replace(input, string.Empty);
            }

            foreach (var attr in BlackListAttributes)
            {
                var attrRegex = new Regex($"{attr}\\s*=\\s*['\"].*?['\"]", RegexOptions.IgnoreCase);
                input = attrRegex.Replace(input, string.Empty);
            }

            var jsLinkRegex = new Regex(@"href\s*=\s*['""]javascript:[^'""]*['""]", RegexOptions.IgnoreCase);
            input = jsLinkRegex.Replace(input, string.Empty);

            // Remove inline event handlers
            var inlineEventHandlerRegex = new Regex(@"\s*on\w+\s*=\s*['""][^'""]*['""]", RegexOptions.IgnoreCase);
            input = inlineEventHandlerRegex.Replace(input, string.Empty);

            // Additional regex to remove potentially dangerous content
            var dangerousRegex = new Regex(@"(<script[^>]*>.*?</script>|<[^>]*javascript:[^>]*>)", RegexOptions.IgnoreCase);
            input = dangerousRegex.Replace(input, string.Empty);

            if (ignoreLinks)
            {
                var plainLinkRegex = new Regex(@"(http|https):\/\/[^\s<>]+", RegexOptions.IgnoreCase);
                input = plainLinkRegex.Replace(input, string.Empty);
            }

            if (encodeInput)
                return HttpUtility.HtmlEncode(input).Trim();
            else
                return input.Trim();
        }

        public static Dictionary<string, object> SanitizeJsonFields(Dictionary<string, object> data, bool encodeInput,
            bool allowLinks, bool onlyEncode = false)
        {
            foreach (var key in data.Keys)
            {
                if (data[key] is string input && !string.IsNullOrEmpty(input))
                {
                    data[key] = Sanitize(input, encodeInput, allowLinks, onlyEncode);
                }
                else if (data[key] is List<string> list && list != null)
                {
                    List<string> result = [];
                    foreach (var item in list)
                        result.Add(Sanitize(item, encodeInput, allowLinks, onlyEncode));

                    data[key] = result;
                }
                else if (data[key] is Dictionary<string, string> dict && dict != null)
                {
                    Dictionary<string, string> result = [];
                    foreach (var dictKey in dict.Keys)
                        result[dictKey] = Sanitize(dict[dictKey], encodeInput, allowLinks, onlyEncode);

                    data[key] = result;
                }
                else if (data[key] is Dictionary<string, object> nestedDictionary)
                {
                    data[key] = SanitizeJsonFields(nestedDictionary, encodeInput, allowLinks, onlyEncode);
                }
            }

            return data;
        }
    }
}
