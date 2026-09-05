using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Utilities.Services
{
    public abstract class SchedulerBase(IServiceProvider serviceProvider, TimeSpan frequency) : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly TimeSpan _frequency = frequency;

        protected abstract Task HandleAsync(IServiceProvider scopedProvider);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    await HandleAsync(scope.ServiceProvider);
                }
                catch (Exception ex)
                {
                    //TODO: Log or report exception (e.g. SentrySdk.CaptureException(ex))
                    Console.WriteLine(ex.Message);
                }

                if (_frequency == TimeSpan.Zero)
                    break;

                try
                {
                    await Task.Delay(_frequency, stoppingToken);
                }
                catch (Exception)
                {
                    break;
                }
            }
        }
    }
}