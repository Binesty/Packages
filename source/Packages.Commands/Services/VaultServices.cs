using System.Net.Http.Json;

namespace Packages.Commands.Services
{
    public class VaultServices
    {
        private readonly HttpClient _httpClient;
        private const string secretVersion = "v1";
        private const string secretPath = "packages-commands";

        public VaultServices(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<Secret> GetFromVault()
        {
            var environment = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "development";

            var data = await _httpClient.GetFromJsonAsync<Secret>($"{secretVersion}/{environment}/{secretPath}");

            return data ?? throw new Exception("Not found secrets");
        }
    }
}