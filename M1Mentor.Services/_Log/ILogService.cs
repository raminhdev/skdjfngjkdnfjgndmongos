using M1Mentor.Services._Log.DTOs.Updates;

namespace M1Mentor.Services._Log
{
    public interface ILogService
    {
        Task CaptureLogAsync(LogUpdate update);
        Task CaptureRequestLogAsync(RequestLogUpdate update, string publicKey, string phone);
        Task HardDeleteRequestLogsAsync();
        Task HardDeleteLogsLogsAsync();
    }
}
