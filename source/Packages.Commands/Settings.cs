using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace Packages.Commands
{    
    internal class Settings
    {
        public readonly IContract Contract;

        public BrokerSettings BrokerSettings { get; }

        public CosmosSettings CosmosSettings { get; }

        public Settings(IContract contract)
        {
            Contract = contract;
            string tenantId = "";
            string clientId = "";
            string clientSecret = "";
            string keyVaultAddress = "";

            var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            var secretClient = new SecretClient(new Uri(keyVaultAddress), clientSecretCredential);

            var broker_host = secretClient.GetSecretAsync("broker-host")
                                          .GetAwaiter()
                                          .GetResult()
                                          .Value;

            var broker_user = secretClient.GetSecretAsync("broker-user")
                                          .GetAwaiter()
                                          .GetResult()
                                          .Value;

            var broker_password = secretClient.GetSecretAsync("broker-password")
                                              .GetAwaiter()
                                              .GetResult()
                                              .Value;

            var broker_port = secretClient.GetSecretAsync("broker-port")
                                          .GetAwaiter()
                                          .GetResult()
                                          .Value;

            var cosmosdb_primary_key = secretClient.GetSecretAsync("cosmosdb-primary-key")
                                                   .GetAwaiter()
                                                   .GetResult()
                                                   .Value;

            var cosmosdb_url = secretClient.GetSecretAsync("cosmosdb-url")
                                           .GetAwaiter()
                                           .GetResult()
                                           .Value;

            BrokerSettings = new BrokerSettings(broker_host.Value, broker_user.Value, broker_password.Value, int.Parse(broker_port.Value));
            CosmosSettings = new CosmosSettings(Contract.Name, cosmosdb_primary_key.Value, cosmosdb_url.Value);
        }
    }

    internal record BrokerSettings(string Host, string User, string Password, int Port);

    internal record CosmosSettings(string Database, string PrimaryKey, string EndPoint);
}