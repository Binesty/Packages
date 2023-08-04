using Packages.Commands;
using Packages.Commands.Extensions;

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
                                         .AddPackagesCommands();
                             })
                            .Build();

            host.Run();
        }
    }
}