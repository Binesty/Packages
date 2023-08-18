using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Packages.Microservices.Services;

namespace Packages.Microservices.Extensions
{
    public static class PackagesMicroservicesExtensions
    {
        public static OptionsBuilder<Settings> AddPackagesMicroservices(this OptionsBuilder<Settings> optionsBuilder)
        {
            optionsBuilder.Services.AddSingleton<VaultServices>();
            optionsBuilder.Services.AddSingleton<CatalogsServices>();

            optionsBuilder.Services.AddApiServices();

            var serviceProvider = optionsBuilder.Services.BuildServiceProvider();
            var vaultServices = serviceProvider.GetRequiredService<VaultServices>();

            CatalogsServices.Current = serviceProvider.GetRequiredService<CatalogsServices>();

            Secret.Loaded = vaultServices.GetFromVault()
                                         .GetAwaiter()
                                         .GetResult()
                                         .Data ?? throw new Exception("Not found secrets");

            return optionsBuilder;
        }
    }
}