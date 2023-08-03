using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Packages.Commands.Services;

namespace Packages.Commands.Extensions
{
    public static class PackagesCommandsExtensions
    {
        public static OptionsBuilder<Settings> AddPackagesCommands(this OptionsBuilder<Settings> optionsBuilder)
        {
            optionsBuilder.Services.AddSingleton<VaultServices>();
            optionsBuilder.Services.AddSingleton<CatalogsServices>();

            optionsBuilder.Services.AddApiServices();

            var serviceProvider = optionsBuilder.Services.BuildServiceProvider();
            var vaultServices = serviceProvider.GetRequiredService<VaultServices>();

            Secret.Loaded = vaultServices.GetFromVault()
                                         .GetAwaiter()
                                         .GetResult()
                                         .Data ?? throw new Exception("Not found secrets");

            return optionsBuilder;
        }
    }
}