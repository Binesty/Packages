using Packages.Commands;

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

                                services.AddOptions<CommandsOptions>()
                                        .BindConfiguration(CommandsOptions.SectionName)
                                        .ValidateFluently()
                                        .ValidateOnStart();
                             })
                            .Build();

            host.Run();
        }
    }
}