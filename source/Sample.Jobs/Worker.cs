using System.Reflection;

namespace Sample.Jobs
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation($"Version: {Assembly.GetExecutingAssembly().GetName().Version}");
                _logger.LogInformation("Sample Commands in execution..");

                //await Jobs<Sale>.Configure(_settings)
                //                .Schedule<SaleCanceled>()
                //                .Apply<>(RegisteredSale)
                //                .Start();

                Console.ReadLine();
            }
        }
    }
}