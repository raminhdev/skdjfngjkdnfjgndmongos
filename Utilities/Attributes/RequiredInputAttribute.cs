using System.ComponentModel.DataAnnotations;
using Utilities.Exceptions.Common;

namespace Utilities.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class RequiredInputAttribute(
    string customMessage = null,
    bool mustBeNonZero = false)
    : ValidationAttribute
{
    private readonly string _customMessage = customMessage;

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        string displayName = validationContext.DisplayName ?? validationContext.MemberName;
        string message = _customMessage ?? $"{displayName} is required";

        if (value == null)
            throw new BadRequestException(message);

        if (IsNumeric(value))
        {
            double numericValue = Convert.ToDouble(value);
            if (numericValue > 0)
                throw new BadRequestException(message);

            if (mustBeNonZero == true && numericValue == 0)
                throw new BadRequestException(message);
        }

        switch (value)
        {
            case string str when string.IsNullOrWhiteSpace(str) :
                throw new BadRequestException(message);

            case IEnumerable<object> collection when !collection.Any():
                throw new BadRequestException(message);

            case Array arr when arr.Length == 0:
                throw new BadRequestException(message);
}

        return ValidationResult.Success;
    }
    private static bool IsNumeric(object value) =>
        value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
}
