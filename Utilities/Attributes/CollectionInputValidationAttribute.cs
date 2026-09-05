using System.Collections;
using System.ComponentModel.DataAnnotations;
using Utilities.Enums;
using Utilities.Exceptions.Common;
using Utilities.Extensions;

namespace Utilities.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public class CollectionInputAttribute(
    bool isRequired = true,
    bool allowEmpty = false,
    bool enforceUnique = false,
    int minSize = 0,
    int maxSize = int.MaxValue,
    bool noNullElements = false,
    bool noEmptyStrings = false,
    string customMessage = null
) : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        string displayName = validationContext.DisplayName ?? validationContext.MemberName;
        string msg(string fallback) => customMessage ?? $"{displayName} {fallback}";

        if (value == null)
        {
            if (isRequired)
                throw new BadRequestException(msg(ApiResultStatusCode.Required.ToDisplay()));
            return ValidationResult.Success;
        }

        if (value is not IEnumerable enumerable)
            throw new BadRequestException($"{displayName} must be a collection");

        var list = enumerable.Cast<object>().ToList();
        int count = list.Count;

        if (count == 0 && (!allowEmpty || minSize > 0))
            throw new BadRequestException(msg("cannot be empty"));

        if (count < minSize)
            throw new BadRequestException(msg($"must have at least {minSize} items"));

        if (count > maxSize)
            throw new BadRequestException(msg($"cannot have more than {maxSize} items"));

        if (noNullElements && list.Any(item => item == null))
            throw new BadRequestException(msg("cannot contain null values"));

        if (noEmptyStrings)
        {
            bool hasNonStrings = list.Any(item => item != null && item is not string);
            if (hasNonStrings)
                throw new InvalidOperationException(
                    $"{nameof(CollectionInputAttribute)} with {nameof(noEmptyStrings)}=true should only be used on string collections");

            if (list.Any(item => item is string str && string.IsNullOrWhiteSpace(str)))
                throw new BadRequestException(msg("cannot contain empty or whitespace strings"));
        }

        if (enforceUnique)
        {
            bool isStringCollection = list.All(item => item == null || item is string);
            int distinctCount = isStringCollection
                ? list.Cast<string>().Distinct(StringComparer.Ordinal).Count()
                : list.Distinct().Count();

            if (distinctCount != count)
                throw new BadRequestException(msg("cannot contain duplicate values"));
        }

        return ValidationResult.Success;
    }
}
