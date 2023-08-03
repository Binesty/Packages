using Packages.Commands.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Packages.Commands.Extensions
{
    public static class ServicesExtension
    {
        private static readonly SocketsHttpHandler DefaultSocketsHttpHandler = new()
        {
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
        };

        public static IServiceCollection AddApiServices(this IServiceCollection services)
        {
            services.AddVaultServices();
            services.AddCatalogsServices();

            return services;
        }

        private static IServiceCollection AddVaultServices(this IServiceCollection services)
        {
            services.AddHttpClient<VaultServices>((servicesProvider, httpClient) =>
            {
                var settings = servicesProvider.GetRequiredService<IOptions<Settings>>().Value;

                httpClient.DefaultRequestHeaders.Add("X-Vault-Token", settings.VaultToken);
                httpClient.BaseAddress = new Uri(settings.VaultAddress);
            })
            .ConfigurePrimaryHttpMessageHandler(() => { return DefaultSocketsHttpHandler; });

            return services;
        }

        private static IServiceCollection AddCatalogsServices(this IServiceCollection services)
        {
            services.AddHttpClient<CatalogsServices>((servicesProvider, httpClient) =>
            {
                var settings = servicesProvider.GetRequiredService<IOptions<Settings>>().Value;                
                httpClient.BaseAddress = new Uri(settings.CatalogsAddress);
            })
            .ConfigurePrimaryHttpMessageHandler(() => { return DefaultSocketsHttpHandler; });

            return services;
        }
    }
}