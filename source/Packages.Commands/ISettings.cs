namespace Packages.Commands
{
    public interface ISettings
    {
        public string Name { get; }

        public string Description { get; }

        public BrokerSettings BrokerSettings { get; }

        public CosmosSettings CosmosSettings { get; }
    }

    public record BrokerSettings(string Host, string User, string Password, int Port);

    public record CosmosSettings(string Database, string PrimaryKey, string EndPoint);
}