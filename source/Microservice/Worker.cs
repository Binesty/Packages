using Microservice.Domain;
using Microservice.Domain.Commands;
using Microservice.Domain.Replications;
using Microsoft.Extensions.Options;
using Packages.Commands;

namespace Microservice
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IOptions<CommandsOptions> _options; 

        public Worker(ILogger<Worker> logger, IOptions<CommandsOptions> options)
        {
            _logger = logger;
            _options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Started microservice");

            while (!stoppingToken.IsCancellationRequested)
            {
                await Microservice<Sale>.Configure(_options)
                                        .Execute<Sell>()
                                        .Apply<CarEndManufacturing>()
                                        .Start();

                await Task.Delay(5000, stoppingToken);
                await Task.Run(() => Simulator.Start(_options), stoppingToken);
            }
        }
    }
}