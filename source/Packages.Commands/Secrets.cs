using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Packages.Commands
{
    public class Secrets
    {
        public string CatalogEndPoint { get; init; } = string.Empty;
        public string CosmosPrimaryKey { get; init; } = string.Empty;
        public string CosmosEndPoint { get; init; } = string.Empty;
        public string RabbitHost { get; init; } = string.Empty;
        public string RabbitUser { get; init; } = string.Empty;
        public string RabbitPassword { get; init; } = string.Empty;
        public int RabbitPort { get; init; }

        public static Secrets Load(IOptions<Settings> _settings)
        {
            string version = "v1";
            string pathSecret = "packages-commands";

            using var httpClient = new HttpClient() { BaseAddress = new Uri(_settings.Value.VaultAddress) };

            httpClient.DefaultRequestHeaders.Add("X-Vault-Token", _settings.Value.VaultToken);
            string? environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")?.ToLower();

            var result = httpClient.GetFromJsonAsync<JsonNode>($"{version}/{environment}/{pathSecret}")
                                   .GetAwaiter()
                                   .GetResult() ?? throw new Exception("Not get secrets from the vault");

            var secrets = result["data"]?.Deserialize<Secrets>();

            return secrets ?? throw new Exception("Not found secrets");
        }
    }
}