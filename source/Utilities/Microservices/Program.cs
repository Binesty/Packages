using Microservices.Commands;
using Packages.Commands;
using System.Runtime;

namespace Microservices
{
    internal class Program
    {
        private static Settings Settings => new();

        private static void Main()
        {
            Task.Run(() => Simulator.Start(Settings));

            Microservice<Sale>.Configure(Settings)
                              .Execute<Sell>()
                              .Start();

            Console.ReadLine();
        }
    }
}