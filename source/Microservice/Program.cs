using Packages.Commands;
using Packages.Commands.Extensions;

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
                                         .AddPackagesCommands()
                                         .ValidateFluently()
                                         .ValidateOnStart();
                             })
                            .Build();

            host.Run();
        }
    }
}