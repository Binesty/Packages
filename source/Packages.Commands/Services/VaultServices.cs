using System.Net.Http.Json;

namespace Packages.Commands.Services
{
    public class VaultServices
    {
        private readonly HttpClient _httpClient;        
        private const string _version = "v1";
        private const string _pathSecret = "packages-commands";

        public VaultServices(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<Secret> GetFromVault()
        {
            string? environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")?.ToLower();
            if (string.IsNullOrEmpty(environment))
                environment = "Production";

            var data = await _httpClient.GetFromJsonAsync<Secret>($"{_version}/{environment}/{_pathSecret}");

            return data ?? throw new Exception("Not found secrets");
        }
    }
}
