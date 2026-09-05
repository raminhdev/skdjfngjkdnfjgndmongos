namespace M1Mentor.Services._Log.DTOs.Updates
{
    public class RequestLogUpdate
    {
        public string ControllerName { get; set; }
        public string ApiName { get; set; }
        public string Body { get; set; }
        public string Headers { get; set; }
        public string Query { get; set; }
        public string RoutePath { get; set; }
        public string ClientIP { get; set; }
    }
}
 