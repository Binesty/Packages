using Microservices.Commands;
using Microservices.Replications;
using Packages.Commands;

namespace Microservices
{
    internal class Program
    {
        private static Contract Contract => new();

        private static void Main()
        {
            Task.Run(async () =>
            {
                await Microservice<Sale>.Configure(Contract)
                                            .Execute<Sell>()
                                                .Apply<CarEndManufacturing>()
                                                    .Start();
            });

            Task.Run(() => Simulator.Start(Contract));

            Console.ReadLine();
        }
    }
}