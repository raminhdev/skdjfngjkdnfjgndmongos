using System.ComponentModel.DataAnnotations;
using Utilities.Enums;
using Utilities.Exceptions.Common;
using Utilities.Extensions;

namespace Utilities.Attributes
{
    public class InputValidationAttribute(bool isRequired = true, int maxLength = 0, int minLength = 0, int dictValueMaxLength = 0,
         int dictValueMinLength = 0) : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (isRequired)
            {
                if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                    throw new BadRequestException($"{validationContext.DisplayName} {ApiResultStatusCode.Required.ToDisplay()}");

                //else if (value is decimal decimalValue && decimalValue == 0)
                //    throw new BadRequestException($"{validationContext.DisplayName} {ApiResultStatusCode.Required.ToDisplay()}");
            }

            if (maxLength != 0 && value is not null)
            {
                if (value is string stringValue && stringValue.Length > maxLength)
                    throw new BadRequestException($"{validationContext.DisplayName} field length is too much");

                else if (value is List<string> listValue && listValue.Any(q => q.Length > maxLength))
                    throw new BadRequestException($"{validationContext.DisplayName} field length is too much");

                else if (value is Dictionary<string, string> dictValue && dictValue.Keys.Any(q => q.Length > maxLength))
                    throw new BadRequestException($"{validationContext.DisplayName} field length is too much");
            }

            if (minLength != 0 && value is not null)
            {
                if (value is string stringValue && stringValue.Length < minLength)
                    throw new BadRequestException($"{validationContext.DisplayName} field length is too short");

                else if (value is List<string> listValue && listValue.Any(q => q.Length < minLength))
                    throw new BadRequestException($"{validationContext.DisplayName} field length is too short");

                else if (value is Dictionary<string, string> dictValue && dictValue.Keys.Any(q => q.Length < minLength))
                    throw new BadRequestException($"{validationContext.DisplayName} field length is too short");
            }

            if (dictValueMaxLength != 0 && value is Dictionary<string, string> dictValue1 && dictValue1.Values.Any(q => q.Length > dictValueMaxLength))
                throw new BadRequestException($"{validationContext.DisplayName} field length is too much");

            if (dictValueMinLength != 0 && value is Dictionary<string, string> dictValue2 && dictValue2.Values.Any(q => q.Length > dictValueMinLength))
                throw new BadRequestException($"{validationContext.DisplayName} field length is too short");

            return ValidationResult.Success;
        }
    }
}
