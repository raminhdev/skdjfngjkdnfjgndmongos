using Utilities.Exceptions.Common;

namespace Utilities.Extensions
{
    public static class GenericTypeExtension
    {
        public static void MapPropertiesExtension<TSource, TTarget>(TSource source, TTarget target)
        {
            var sourceProps = typeof(TSource).GetProperties();
            var targetProps = typeof(TTarget).GetProperties().ToDictionary(p => p.Name);

            foreach (var sourceProp in sourceProps)
            {
                if (targetProps.TryGetValue(sourceProp.Name, out var targetProp))
                {
                    var value = sourceProp.GetValue(source);
                    if (value != null && targetProp.CanWrite)
                    {
                        targetProp.SetValue(target, value);
                    }
                }
            }
        }

        public static T RemoveProperties<T>(this T dto, List<string> propertyNames)
        {
            var dtoType = dto.GetType();

            foreach (var property in dtoType.GetProperties())
            {
                if (propertyNames.Any(propertyName => propertyName.Equals(property.Name, StringComparison.CurrentCultureIgnoreCase)))
                    property.SetValue(dto, null, null);
            }

            return dto;
        }

        public static T CheckNotNull<T>(this T obj, string message = null)
            where T : class
        {
            if (obj is null)
            {
                message ??= $"{typeof(T).Name} Not Found!";
                throw new BadRequestException(message);
            }
            return obj;
        }

        public static async Task<T> CheckNotNullAsync<T>(this Task<T> obj, string message = null)
            where T : class
        {
            var result = await obj;
            if (result is null)
            {
                message ??= $"{typeof(T).Name} Not Found!";
                throw new BadRequestException(message);
            }
            return await obj;
        }

        public static T CheckIsNull<T>(this T obj, string message = null)
            where T : class
        {
            if (obj is not null)
                throw new BadRequestException(message);

            return obj;
        }

        public static async Task<T> CheckIsNullAsync<T>(this Task<T> obj, string message = null)
            where T : class
        {
            var result = await obj;
            if (result is not null)
                throw new BadRequestException(message);

            return await obj;
        }

        public static List<T> CheckNotEmpty<T>(this List<T> obj, string message = null)
            where T : class
        {
            if (obj is null || obj.Count <= 0)
            {
                message ??= $"{typeof(T).Name} Not Found!";
                throw new BadRequestException(message);
            }
            return obj;
        }

        public static async Task<List<T>> CheckNotEmptyAsync<T>(this Task<List<T>> obj, string message = null)
            where T : class
        {
            var result = await obj;
            if (result is null || result.Count <= 0)
            {
                message ??= $"{typeof(T).Name} Not Found!";
                throw new BadRequestException(message);
            }
            return await obj;
        }

        public static List<T> CheckIsEmpty<T>(this List<T> obj, string message = null)
            where T : class
        {
            if (obj != null && obj.Count > 0)
                throw new BadRequestException(message);

            return obj;
        }

        public static async Task<List<T>> CheckIsEmptyAsync<T>(this Task<List<T>> obj, string message = null)
            where T : class
        {
            var result = await obj;
            if (result != null && result.Count > 0)
                throw new BadRequestException(message);

            return await obj;
        }

        public static void CheckIsTrue<T>(this bool obj, string message = null)
            where T : class
        {
            if (!obj)
                throw new BadRequestException(message);
        }

        public static async void CheckIsTrueAsync<T>(this Task<bool> obj, string message = null)
            where T : class
        {
            var result = await obj;
            if (!result)
                throw new BadRequestException(message);
        }

        public static void CheckNotTrue<T>(this bool obj, string message = null)
            where T : class
        {
            if (obj)
                throw new BadRequestException(message);
        }

        public static async void CheckNotTrueAsync<T>(this Task<bool> obj, string message = null)
            where T : class
        {
            var result = await obj;
            if (result)
                throw new BadRequestException(message);
        }
    }
}
