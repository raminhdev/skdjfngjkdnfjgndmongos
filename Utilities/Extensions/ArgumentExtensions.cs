using System.Collections;

namespace Utilities.Extensions
{
    public static class ArgumentExtensions
    {
        public static void NotNull<T>(this T obj, string name, string message = null)
            where T : class
        {
            if (obj is null)
                throw new ArgumentNullException($"{name} : {typeof(T)}", message);
        }

        public static void NotNull<T>(this T? obj, string name, string message = null)
            where T : struct
        {
            if (!obj.HasValue)
                throw new ArgumentNullException($"{name} : {typeof(T)}", message);
        }

        public static void NotEmpty<T>(this T obj, string name, string message = null, T defaultValue = null)
            where T : class
        {
            if (obj == defaultValue
                || obj is string str && string.IsNullOrWhiteSpace(str)
                || obj is IEnumerable list && !list.Cast<object>().Any())
            {
                throw new ArgumentException("Argument is empty : " + message, $"{name} : {typeof(T)}");
            }
        }

        public static void NotGreaterThan<T>(this T arg, T target, string name, string message = null)
            where T : IComparable
        {
            var comparisonResult = arg.CompareTo(target);
            if (comparisonResult <= 0)
                throw new ArgumentException($"Argument is less than {target} : {message}",
                    paramName: $"{name} : {typeof(T)}");
        }
    }
}