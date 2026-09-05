using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Utilities.Services
{
    public abstract class OldSchedulerBase : BackgroundService
    {
        private readonly IServiceProvider servcieProvider;
        private readonly int frequency;

        protected IServiceScope serviceScope;

        public OldSchedulerBase(IServiceProvider serviceProvider, int frequency = 100)
        {
            this.servcieProvider = serviceProvider;

            this.frequency = frequency;

            serviceScope = serviceProvider.CreateScope();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (true)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    await handle();
                }
                catch (Exception e)
                {
                    //SentrySdk.CaptureException(e);
                }

                if (frequency == 0)
                    break;

                await Task.Delay(frequency);

            }
        }

        protected virtual Task handle()
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
            serviceScope.Dispose();

            base.Dispose();
        }
    }
}
