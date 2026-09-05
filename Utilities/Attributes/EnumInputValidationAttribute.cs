using System.ComponentModel.DataAnnotations;
using Utilities.Exceptions.Common;

/// <summary>
/// Validates that the decorated property is a valid value of its enum type.
/// Automatically infers the enum type from the property using reflection.
/// Supports nullable enums and string inputs that match enum names (case-insensitive).
/// </summary>
/// <param name="IsRequired">
/// If true (default), the value is required and null will trigger a validation failure.
/// If false, null values are allowed and considered valid.
/// </param>
/// <param name="CustomMessage">
/// Optional custom error message to display on validation failure. If null, a default message is generated using the property name.
/// </param>
[AttributeUsage(AttributeTargets.Property)]
public class EnumInputValidationAttribute : ValidationAttribute
{
    public bool IsRequired { get; set; } = true;
    public string CustomMessage { get; set; }

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        var property = validationContext.ObjectType.GetProperty(validationContext.MemberName);

        Type enumType = (property?.PropertyType)
            ?? throw new ArgumentException($"Property '{validationContext.MemberName}' not found.");

        // Support for nullable enums
        if (Nullable.GetUnderlyingType(enumType) is Type underlyingEnumType)
            enumType = underlyingEnumType;

        if (!enumType.IsEnum)
            throw new ArgumentException($"Property '{validationContext.MemberName}' is not an enum type.");

        string displayName = validationContext.DisplayName ?? validationContext.MemberName;
        string message = CustomMessage ?? $"{displayName} is invalid.";

        if (value == null)
        {
            if (IsRequired)
                throw new BadRequestException($"{displayName} is required.");
            else
                return ValidationResult.Success;
        }

        if (value is string strValue)
        {
            if (!Enum.TryParse(enumType, strValue, true, out var parsed) || !Enum.IsDefined(enumType, parsed))
            {
                string validValues = string.Join(", ", Enum.GetNames(enumType));
                return new ValidationResult($"{message} Valid values: {validValues}");
            }
        }
        else if (!Enum.IsDefined(enumType, value))
        {
            string validValues = string.Join(", ", Enum.GetNames(enumType));
            return new ValidationResult($"{message} Valid values: {validValues}");
        }

        return ValidationResult.Success;
    }
}
