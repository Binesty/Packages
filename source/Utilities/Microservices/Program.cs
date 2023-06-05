using Microservices.Commands;
using Microservices.Replications;
using Packages.Commands;

namespace Microservices
{
    internal class Program
    {
        private static Settings Settings => new();

        private static void Main()
        {
            Task.Run(async () =>
            {
                await Microservice<Sale>.Configure(Settings)
                                            .Execute<Sell>()
                                                .Apply<CarEndManufacturing>()
                                                    .Start();
            });

            Task.Run(() => Simulator.Start(Settings));

            Console.ReadLine();
        }
    }
}