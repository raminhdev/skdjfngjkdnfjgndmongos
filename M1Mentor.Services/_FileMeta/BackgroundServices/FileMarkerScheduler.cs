using M1Mentor.Utilities.Services;
using M1Mentor.Services._FileMeta.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Utilities.Services;
using static Utilities.Constants.RegisterMode;

namespace M1Mentor.Services._FileMeta.BackgroundServices
{
    public class FileMarkerScheduler(IServiceProvider serviceProvider)
            : SchedulerBase(serviceProvider, TimeSpan.FromHours(1)), IHostedDependency
    {
        protected override async Task HandleAsync(IServiceProvider scopedProvider)
        {
            var fileMetaService = scopedProvider.GetRequiredService<IFileMetaSchedulerService>();
            await fileMetaService.MarkFilesForDeletionAsync();
        }
    }
}