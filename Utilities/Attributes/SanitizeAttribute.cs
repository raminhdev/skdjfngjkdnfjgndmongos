using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Utilities.Utilities;

namespace Utilities.Attributes
{
    public class SanitizeAttribute(bool encodeInput = true, bool ignoreLinks = false, bool onlyEncode = false) : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            PropertyInfo property = validationContext.ObjectType.GetProperty(validationContext.DisplayName);
            if (property == null)
                return new ValidationResult($"Property '{validationContext.DisplayName}' not found.");

            if (value is string stringValue)
            {
                property.SetValue(validationContext.ObjectInstance, HtmlSanitizer.Sanitize(stringValue, encodeInput, ignoreLinks, onlyEncode), null);
            }
            else if (value is List<string> listValue)
            {
                var data = new List<string>();
                foreach (var item in listValue)
                    data.Add(HtmlSanitizer.Sanitize(item, encodeInput, ignoreLinks, onlyEncode));

                property.SetValue(validationContext.ObjectInstance, data, null);
            }
            else if (value is Dictionary<string, string> dictionaryValue)
            {
                var dictionary = new Dictionary<string, string>();
                foreach (var key in dictionaryValue.Keys)
                    dictionary[key] = HtmlSanitizer.Sanitize(dictionaryValue[key], encodeInput, ignoreLinks, onlyEncode);

                property.SetValue(validationContext.ObjectInstance, dictionary, null);
            }

            return ValidationResult.Success;
        }
    }
}
