using System.ComponentModel.DataAnnotations;
using Utilities.Exceptions.Common;

namespace Utilities.Attributes
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="isRequired"></param>
    /// <param name="checkOffset"></param>
    /// <param name="mustBeFuture"></param>
    /// <param name="mustBePast"></param>
    /// <param name="minOffsetDays"></param>
    /// <param name="maxOffsetDays"></param>
    /// <param name="customMessage"></param>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
        public class ValidatedDateAttribute(
            bool isRequired = true,
            bool checkOffset = false,
            bool mustBeFuture = false,
            bool mustBePast = false,
            int minOffsetDays = int.MinValue, 
            int maxOffsetDays = int.MaxValue,
            string customMessage = null)
            : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext context)
        {
            if (mustBeFuture && mustBePast)
                throw new BaseException("Cannot require both future and past dates.");

            if (checkOffset && (mustBeFuture || mustBePast))
                throw new BaseException("Cannot combine checkOffset with mustBeFuture or mustBePast.");

            if (checkOffset && minOffsetDays == int.MinValue && maxOffsetDays == int.MaxValue)
                throw new BaseException(
                    $"{nameof(checkOffset)} is true but neither {nameof(minOffsetDays)} nor {nameof(maxOffsetDays)} was set.");

            string displayName = context.DisplayName ?? context.MemberName!;
            string msg(string fallback)
                => !string.IsNullOrWhiteSpace(customMessage) ? customMessage! : $"{displayName} {fallback}";

            if (value is null)
            {
                if (isRequired)
                    throw new BadRequestException(msg("is required"));
                return ValidationResult.Success;
            }

            DateTimeOffset dtoValue = value switch
            {
                DateTimeOffset dto => dto,
                DateTime dt when dt.Kind == DateTimeKind.Utc => new DateTimeOffset(dt, TimeSpan.Zero),
                DateTime dt when dt.Kind == DateTimeKind.Local => new DateTimeOffset(dt, TimeZoneInfo.Local.GetUtcOffset(dt)),
                DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc), TimeSpan.Zero),
                _ => throw new BadRequestException($"{displayName} must be a valid date")
            };

            if (isRequired && dtoValue == default)
                throw new BadRequestException(msg("must be a valid date"));

            var now = DateTimeOffset.UtcNow;

            if (mustBeFuture && dtoValue <= now)
                throw new BadRequestException(msg("must be in the future"));

            if (mustBePast && dtoValue >= now)
                throw new BadRequestException(msg("must be in the past"));

            if (checkOffset)
            {
                if (minOffsetDays != int.MinValue)
                {
                    var minBoundary = now.AddDays(minOffsetDays);
                    if (dtoValue < minBoundary)
                        throw new BadRequestException(msg($"must be on or after {minBoundary:yyyy-MM-dd}"));
                }

                if (maxOffsetDays != int.MaxValue)
                {
                    var maxBoundary = now.AddDays(maxOffsetDays);
                    if (dtoValue > maxBoundary)
                        throw new BadRequestException(msg($"must be on or before {maxBoundary:yyyy-MM-dd}"));
                }
            }

            return ValidationResult.Success;
        }
    }
}