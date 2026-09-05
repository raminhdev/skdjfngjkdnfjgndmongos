using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Utilities.MongoDatabase.Contracts;

namespace Utilities.MongoDatabase
{
    /// <summary>Legacy settings class. Use <c>Monjo.MonjoOptions</c> (via <c>services.AddMonjo(configuration)</c>) for new code.</summary>
    [System.Obsolete("Use Monjo.MonjoOptions and services.AddMonjo(configuration). Kept for compatibility.")]
    public class MonjoSettings : IMonjoSettings
    {
        public string ConnectionString { get; set; }
        public string DatabaseName { get; set; }

        public MonjoSettings()
        {
            Configure();
        }

        protected void Configure()
        {
            // Kept for compatibility: new code gets these defaults from Monjo.MongoDB.MongoBsonDefaults.
            BsonSerializer.RegisterSerializer(typeof(decimal), new DecimalSerializer(BsonType.Decimal128));
            BsonSerializer.RegisterSerializer(typeof(decimal?), new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));

            //BsonSerializer.RegisterSerializer(typeof(DateTime), new DateTimeSerializer(DateTimeKind.Local));
        }
    }
}
