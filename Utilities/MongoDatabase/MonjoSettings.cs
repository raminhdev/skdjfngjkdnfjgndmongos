using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Utilities.MongoDatabase.Contracts;

namespace Utilities.MongoDatabase
{
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
            BsonSerializer.RegisterSerializer(typeof(decimal), new DecimalSerializer(BsonType.Decimal128));
            BsonSerializer.RegisterSerializer(typeof(decimal?), new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));

            //BsonSerializer.RegisterSerializer(typeof(DateTime), new DateTimeSerializer(DateTimeKind.Local));
        }
    }
}
