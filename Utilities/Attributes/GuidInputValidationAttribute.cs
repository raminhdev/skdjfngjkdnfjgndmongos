using System.ComponentModel.DataAnnotations;
using Utilities.Exceptions.Common;

namespace Utilities.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public class GuidInputValidationAttribute(
        bool isRequired = true,
        bool allowEmpty = false,
        string errorMessage = null)
        : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            string displayName = validationContext.DisplayName ?? validationContext.MemberName;
            string msg(string fallback) => errorMessage ?? $"{displayName} {fallback}";

            Guid? parsed = value switch
            {
                null => null,
                Guid g => g,
                string s when Guid.TryParse(s, out var g) => g,
                string => throw new BadRequestException(msg("must be a valid GUID")),
                _ => throw new BadRequestException(msg("must be a valid GUID"))
            };

            if (parsed is null)
            {
                if (isRequired)
                    throw new BadRequestException(msg("is required"));
                return ValidationResult.Success;
            }

            if (!allowEmpty && parsed == Guid.Empty)
                throw new BadRequestException(msg("must not be an empty GUID"));

            return ValidationResult.Success;
        }
    }
}