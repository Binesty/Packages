using Packages.Commands;

namespace Microservice
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    services.AddHostedService<Worker>();

                    services.AddOptions<Options>()
                            .BindConfiguration(Options.SectionName);
                        
                })
                .Build();

            host.Run();
        }
    }
}