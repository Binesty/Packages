using Packages.Commands;

namespace Microservices
{
    public class Settings : ISettings
    {
        public string Name => "cars-sale";

        public string Description => "Implementation with Commands of Car Sale";

        BrokerSettings ISettings.BrokerSettings => new("192.168.0.151", "guest", "guest", 32083);

        CosmosSettings ISettings.CosmosSettings => new("binesty", "", "");
    };
}