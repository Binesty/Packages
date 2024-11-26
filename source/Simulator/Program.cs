using Packages.Microservices;
using Packages.Microservices.Extensions;

namespace Simulator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IHost host = Host.CreateDefaultBuilder(args)
                             .ConfigureServices((builder, services) =>
                             {
                                 services.AddHostedService<Worker>();

                                 services.AddOptions<Settings>()
                                         .BindConfiguration(Settings.SectionName)
                                         .AddPackagesMicroservices();
                             })
                            .Build();

            host.Run();
        }
    }
}