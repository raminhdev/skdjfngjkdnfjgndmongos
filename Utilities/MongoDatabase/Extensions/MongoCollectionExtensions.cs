#nullable enable
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using MongoDB.Bson.Serialization.Attributes;

namespace Utilities.MongoDatabase.Extensions
{
    public static class MongoCollectionExtensions
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo> _idPropertyCache = new();

        private static readonly ConcurrentDictionary<Type, Func<object, object?>> _idValueGetterCache = new();

        public static string GetIdentifierName(this object monjoDocument)
        {
            return monjoDocument is null
                ? throw new ArgumentNullException(nameof(monjoDocument))
                : GetIdentifierName(monjoDocument.GetType());
        }

        public static string GetIdentifierName<TDocument>()
            => GetIdentifierName(typeof(TDocument));

        public static object? GetIdentifierValue(this object monjoDocument)
        {
            ArgumentNullException.ThrowIfNull(monjoDocument);

            var type = monjoDocument.GetType();
            var getter = _idValueGetterCache.GetOrAdd(type, BuildIdGetter);
            return getter(monjoDocument);
        }

        private static string GetIdentifierName(Type type)
            => GetIdProperty(type).Name;

        private static PropertyInfo GetIdProperty(Type type)
        {
            return _idPropertyCache.GetOrAdd(type, t =>
            {
                var prop = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(p =>
                        p.GetCustomAttributes(typeof(BsonIdAttribute), inherit: true).Length != 0);

                return prop ?? throw new KeyNotFoundException($"BsonId not found for type '{t.FullName}'.");
            });
        }

        private static Func<object, object?> BuildIdGetter(Type type)
        {
            var idProp = GetIdProperty(type);

            var objParam = Expression.Parameter(typeof(object), "doc");
            var typedObj = Expression.Convert(objParam, type);
            var propAccess = Expression.Property(typedObj, idProp);
            var box = Expression.Convert(propAccess, typeof(object));

            return Expression.Lambda<Func<object, object?>>(box, objParam).Compile();
        }
    }
}