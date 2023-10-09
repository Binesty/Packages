using Microsoft.Extensions.Options;
using Packages.Microservices;
using Packages.Microservices.Commands;
using Sample.Commands.Commands;
using Sample.Commands.Propagations;
using System.Reflection;

namespace Sample.Commands
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
            _logger.LogInformation($"Version: {Assembly.GetExecutingAssembly().GetName().Version}");
            _logger.LogInformation("Sample Commands in execution..");

            await Commands<Sale>.Configure(_settings)
                                .Execute<Sell>()
                                .Apply<CarEndManufacturing>()
                                .Start();

            Console.ReadLine();
        }
    }
}