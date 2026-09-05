using M1Mentor.Domain.Collections;

namespace M1Mentor.Services._Log.DTOs.Updates
{
    public class LogUpdate
    {
        public string Message { get; set; }
        public string PublicKey { get; set; }
        public string FullName { get; set; }
        public string ServiceName { get; set; }
        public string MethodName { get; set; }
        public LogImportance Importance { get; set; }

        //public LogType Type { get; set; }
    }
}
 