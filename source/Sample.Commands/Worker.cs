using Microsoft.Extensions.Options;
using Packages.Microservices;
using Sample.Commands.Domain;
using Sample.Commands.Domain.Commands;
using Sample.Commands.Domain.Replications;

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
            _logger.LogInformation("Sample Commands in execution..");
            await Commands<Sale>.Configure(_settings)
                                .Execute<Sell>()
                                .Apply<CarEndManufacturing>()
                                .Start();

            Console.ReadLine();
        }
    }
}