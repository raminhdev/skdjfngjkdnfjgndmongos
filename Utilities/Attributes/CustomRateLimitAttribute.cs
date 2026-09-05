namespace Utilities.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class CustomRateLimitAttribute(string message = "Too many requests. Try again later",
        int maxAttemptsCount = 5, int periodSeconds = 60, int lockoutDurationMinutes = 5) : Attribute
    {
        public string Message { get; } = message;
        public int MaxAttemptsCount { get; } = maxAttemptsCount;
        public int PeriodSeconds { get; set; } = periodSeconds;
        public int LockoutDurationMinutes { get; } = lockoutDurationMinutes;
    }
}