using M1Mentor.Utilities.Services;
using M1Mentor.Services._FileMeta.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Utilities.Services;
using static Utilities.Constants.RegisterMode;

namespace M1Mentor.Services._FileMeta.BackgroundServices
{
    public class FileCleanupScheduler(IServiceProvider serviceProvider)
        : SchedulerBase(serviceProvider, GetDelayUntilNextRun()), IHostedDependency
    {
        protected override async Task HandleAsync(IServiceProvider scopedProvider)
        {
            var fileMetaService = scopedProvider.GetRequiredService<IFileMetaSchedulerService>();
            await fileMetaService.CleanupMarkedFilesAsync();
        }

        private static TimeSpan GetDelayUntilNextRun()
        {
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(now.Hour >= 3 ? 1 : 0).AddHours(3);
            return nextRun - now;
        }
    }

}
