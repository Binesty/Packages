using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Packages.Commands.Services;

namespace Packages.Commands.Extensions
{
    public static class ServicesExtension
    {
        private static string? _vaultToken;
        private static string? _vaultAddress;
        private static string? _catalogsAddress;

        private static readonly SocketsHttpHandler DefaultSocketsHttpHandler = new()
        {
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
        };

        public static IServiceCollection AddApiServices(this IServiceCollection services)
        {
            LoadEnvironmentVariables();

            services.AddVaultServices();
            services.AddCatalogsServices();

            return services;
        }

        private static void LoadEnvironmentVariables()
        {
            if (Environment.GetEnvironmentVariable("ENVIRONMENT") is null)
            {
                _vaultAddress = "http://vault.binesty.net";
                _vaultToken = "hvs.6wgRqlgOj9aQUqVeDQTyT6Nv";
                _catalogsAddress = "http://api-catalogs.binesty.net";
            }
            else
            {
                _vaultAddress = Environment.GetEnvironmentVariable("VAULT_ADDRESS");
                _vaultToken = Environment.GetEnvironmentVariable("VAULT_TOKEN");
                _catalogsAddress = Environment.GetEnvironmentVariable("CATALOG_ADDRESS");
            }
        }

        private static IServiceCollection AddVaultServices(this IServiceCollection services)
        {
            if (_vaultAddress is null || _vaultToken is null)
                throw new Exception("Vault address or token is null");

            services.AddHttpClient<VaultServices>((servicesProvider, httpClient) =>
            {
                var settings = servicesProvider.GetRequiredService<IOptions<Settings>>().Value;

                httpClient.DefaultRequestHeaders.Add("X-Vault-Token", _vaultToken);
                httpClient.BaseAddress = new Uri(_vaultAddress);
            })
            .ConfigurePrimaryHttpMessageHandler(() => { return DefaultSocketsHttpHandler; });

            return services;
        }

        private static IServiceCollection AddCatalogsServices(this IServiceCollection services)
        {
            if (_catalogsAddress is null)
                throw new Exception("Catalog address is null");

            services.AddHttpClient<CatalogsServices>((servicesProvider, httpClient) =>
            {
                var settings = servicesProvider.GetRequiredService<IOptions<Settings>>().Value;
                httpClient.BaseAddress = new Uri(_catalogsAddress);
            })
            .ConfigurePrimaryHttpMessageHandler(() => { return DefaultSocketsHttpHandler; });

            return services;
        }
    }
}