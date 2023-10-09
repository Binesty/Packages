using Microsoft.Extensions.Options;
using Packages.Microservices;
using Packages.Microservices.Jobs;
using Sample.Commands.Propagations;
using Sample.Jobs.Jobs;
using System.Reflection;

namespace Sample.Jobs
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IOptions<Settings> _settings;

        public Worker(ILogger<Worker> logger, IOptions<Settings> settings)
        {
            _logger = logger;
            _settings = settings;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation($"Version: {Assembly.GetExecutingAssembly().GetName().Version}");
                _logger.LogInformation("Sample Commands in execution..");

                await Jobs<Sale>.Configure(_settings)
                                .Apply<RegisteredSale>()
                                .Schedule<SaleCanceled>()
                                .Start();

                Console.ReadLine();
            }
        }
    }
}