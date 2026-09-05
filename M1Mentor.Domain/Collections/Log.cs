using Utilities.Attributes;
using Utilities.MongoDatabase.Documents;

namespace M1Mentor.Domain.Collections
{
    [MonjoCollectionName("Logs")]
    public class Log : BaseDocument
    {
        public string Message { get; set; }
        public string PublicKey { get; set; }
        public string FullName { get; set; }
        public string ServiceName { get; set; }
        public string MethodName { get; set; }
        public LogImportance Importance { get; set; }
    }
    public enum LogImportance { Low, Medium, High, VeryHigh }
}
