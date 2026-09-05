using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Utilities.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class StringInputValidationAttribute(
    bool isRequired = true,
    int maxLength = 0,
    int minLength = 0,
    bool allowWhiteSpaceOnly = false,
    string regexPattern = null,
    string errorMessage = null
) : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        string displayName = validationContext.DisplayName ?? validationContext.MemberName;
        string strValue = value as string;

        if (string.IsNullOrEmpty(strValue))
        {
            if (isRequired)
                return new ValidationResult(errorMessage ?? $"{displayName} is required.");
            else
                return ValidationResult.Success;
        }

        if (!allowWhiteSpaceOnly && string.IsNullOrWhiteSpace(strValue))
            return new ValidationResult(errorMessage ?? $"{displayName} cannot be whitespace only.");

        if (minLength > 0 && strValue.Length < minLength)
            return new ValidationResult(errorMessage ?? $"{displayName} must be at least {minLength} characters.");

        if (maxLength > 0 && strValue.Length > maxLength)
            return new ValidationResult(errorMessage ?? $"{displayName} must not exceed {maxLength} characters.");

        if (!string.IsNullOrEmpty(regexPattern) && !Regex.IsMatch(strValue, regexPattern))
            return new ValidationResult(errorMessage ?? $"{displayName} format is invalid.");

        return ValidationResult.Success;
    }
}