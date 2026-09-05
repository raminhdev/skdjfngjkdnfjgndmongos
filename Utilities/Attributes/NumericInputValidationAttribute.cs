using System.ComponentModel.DataAnnotations;
using Utilities.Enums;
using Utilities.Exceptions.Common;
using Utilities.Extensions;

namespace Utilities.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class NumericInputValidationAttribute(
    bool isRequired = false,
    bool mustBePositive = true,
    bool mustBeNonZero = false,
    double min = double.MinValue,
    double max = double.MaxValue,
    string customMessage = null
) : ValidationAttribute
{
    private readonly bool _isRequired = isRequired;
    private readonly bool _mustBePositive = mustBePositive;
    private readonly bool _mustBeNonZero = mustBeNonZero;
    private readonly double _min = min;
    private readonly double _max = max;
    private readonly string _customMessage = customMessage;

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        string displayName = validationContext.DisplayName ?? validationContext.MemberName;

        if (value == null)
        {
            if (_isRequired)
                throw new BadRequestException(_customMessage ?? $"{displayName} {ApiResultStatusCode.Required.ToDisplay()}");

            return ValidationResult.Success;
        }

        // Not numeric? Reject
        if (!IsNumeric(value))
            throw new BadRequestException(_customMessage ?? $"{displayName} must be a valid number");

        double numericValue = Convert.ToDouble(value);

        if (_mustBeNonZero && numericValue == 0)
            throw new BadRequestException(_customMessage ?? $"{displayName} must not be zero");

        if (_mustBePositive && numericValue < 0)
            throw new BadRequestException(_customMessage ?? $"{displayName} must be a positive number");

        if (numericValue < _min)
            throw new BadRequestException(_customMessage ?? $"{displayName} must be at least {_min}");

        if (numericValue > _max)
            throw new BadRequestException(_customMessage ?? $"{displayName} must be at most {_max}");

        return ValidationResult.Success;
    }

    private static bool IsNumeric(object value) =>
        value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
}