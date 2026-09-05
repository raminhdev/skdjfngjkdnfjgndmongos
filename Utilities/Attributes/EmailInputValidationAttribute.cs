using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Utilities.Enums;
using Utilities.Exceptions.Common;
using Utilities.Extensions;

namespace Utilities.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class EmailInputAttribute(
    bool isRequired = true,
    string customMessage = null,
    bool throwException = true
) : ValidationAttribute
{
    private static readonly string EmailPattern =
        @"^[\w!#$%&'*+\-/=?^_`{|}~]+(\.[\w!#$%&'*+\-/=?^_`{|}~]+)*" +
        @"@([a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$";

    private readonly Regex _emailRegex = new Regex(EmailPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        string displayName = validationContext.DisplayName ?? validationContext.MemberName;
        string msg(string fallback) => customMessage ?? $"{displayName} {fallback}";

        if (value == null)
        {
            if (isRequired)
                return Fail(msg(ApiResultStatusCode.Required.ToDisplay()));
            return ValidationResult.Success;
        }

        if (value is not string email)
            return Fail($"{displayName} must be a string.");

        email = email.Trim();

        if (string.IsNullOrEmpty(email))
        {
            if (isRequired)
                return Fail(msg(ApiResultStatusCode.Required.ToDisplay()));
            return ValidationResult.Success;
        }

        if (!_emailRegex.IsMatch(email))
            return Fail(msg("must be a valid email address."));

        return ValidationResult.Success;
    }

    private ValidationResult Fail(string message)
    {
        if (throwException)
            throw new BadRequestException(message);
        return new ValidationResult(message);
    }
}