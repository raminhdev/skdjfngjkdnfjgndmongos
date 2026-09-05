namespace M1Mentor.Api.Utilities.Middlewares
{
    public static class ApplicationControllerBuilderExtensions
    {
        public static void UseSecurityStamp(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<SecurityStampMiddleware>();
        }
        public static void UseProductionCors(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<ProductionCorsMiddleware>();
        }
        public static void UseLogger(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<LoggingMiddleware>();
        }
    }
}