using Utilities.Attributes;
using Utilities.MongoDatabase.Documents;

namespace M1Mentor.Domain.Collections
{
    [MonjoCollectionName("RequestLogs")]
    public class RequestLog : BaseDocument
    {
        public string ControllerName { get; set; }
        public string ApiName { get; set; }
        public string Body { get; set; }
        public string Headers { get; set; }
        public string Query { get; set; }
        public string RoutePath { get; set; }
        public string ClientIP { get; set; }
        public string PublicKey { get; set; }
        public string Phone { get; set; }
    }
}
