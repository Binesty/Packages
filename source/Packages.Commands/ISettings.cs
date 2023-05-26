namespace Packages.Commands
{
    public interface ISettings
    {
        public string Name { get; }

        public string Description { get; }

        public Infrastructure Infrastructure { get; }
    }

    public record Infrastructure(string BrokerHost, string BrokerUser, string BrokerPassword, int BrokerPort);
}