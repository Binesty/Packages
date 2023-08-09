using System.Net.Http.Json;

namespace Packages.Commands.Services
{
    public class VaultServices
    {
        private readonly HttpClient _httpClient;

        public VaultServices(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<Secret> GetFromVault()
        {
            string path = Environment.GetEnvironmentVariable("VAULT_SECRET") ?? "/v1/development/packages-commands";

            var data = await _httpClient.GetFromJsonAsync<Secret>($"{path}");

            return data ?? throw new Exception("Not found secrets");
        }
    }
}