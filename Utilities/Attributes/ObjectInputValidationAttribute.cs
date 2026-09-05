using System.ComponentModel.DataAnnotations;
using Utilities.Enums;
using Utilities.Exceptions.Common;
using Utilities.Extensions;

namespace Utilities.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public class ObjectInputValidationAttribute(
    bool isRequired = true,
    bool validateNestedProperties = true,
    string customMessage = null
) : ValidationAttribute
{
    private readonly bool _isRequired = isRequired;
    private readonly bool _validateNested = validateNestedProperties;
    private readonly string _customMessage = customMessage;

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        string displayName = validationContext.DisplayName ?? validationContext.MemberName;
        string msg(string fallback) => _customMessage ?? $"{displayName} {fallback}";

        if (value == null)
        {
            if (_isRequired)
                throw new BadRequestException(msg(ApiResultStatusCode.Required.ToDisplay()));
            return ValidationResult.Success;
        }

        // Run validation on nested object if requested
        if (_validateNested)
        {
            var results = new List<ValidationResult>();
            var context = new ValidationContext(value);

            bool isValid = Validator.TryValidateObject(
                value,
                context,
                results,
                validateAllProperties: true
            );

            if (!isValid)
            {
                string errors = string.Join(" | ", results.Select(r => r.ErrorMessage));
                throw new BadRequestException(msg($"contains invalid values: {errors}"));
            }
        }

        return ValidationResult.Success;
    }
}
