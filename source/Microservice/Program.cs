using Packages.Microservices;
using Packages.Microservices.Extensions;

namespace Microservice
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
                                         .AddPackagesMicroservices()
                                         .ValidateFluently()
                                         .ValidateOnStart();
                             })
                            .Build();

            host.Run();
        }
    }
}