using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Monjo.MongoDB
{
    /// <summary>
    /// Registers the BSON serializer defaults that the pre-existing MonjoSettings used to register
    /// (decimal → Decimal128). Idempotent and executed once per process, before any client is built.
    /// </summary>
    internal static class MongoBsonDefaults
    {
        private static readonly object _lock = new();
        private static int _registered;

        public static void Register()
        {
            if (System.Threading.Interlocked.CompareExchange(ref _registered, 1, 0) == 1)
                return;

            lock (_lock)
            {
                if (_registered == 1)
                    return;

                if (BsonSerializer.LookupSerializer(typeof(decimal)) is not DecimalSerializer)
                    BsonSerializer.RegisterSerializer(typeof(decimal), new DecimalSerializer(BsonType.Decimal128));

                if (BsonSerializer.LookupSerializer(typeof(decimal?)) is not NullableSerializer<decimal>)
                    BsonSerializer.RegisterSerializer(
                        typeof(decimal?),
                        new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));

                _registered = 1;
            }
        }
    }
}
