using Microservice.Domain;
using Microservice.Domain.Commands;
using Microservice.Domain.Replications;
using Microsoft.Extensions.Options;
using Packages.Microservices;

namespace Microservice
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
            _logger.LogInformation("Microservices in execution..");
            await Microservice<Sale>.Configure(_settings)
                                    .Execute<Sell>()
                                    .Apply<CarEndManufacturing>()
                                    .Start();

            Console.ReadLine();
        }
    }
}