namespace Packages.Commands
{
    public class Options
    {
        public const string SectionName = "Options";

        public string Name { get; init; }
        public string Description { get; init; }

        public string CosmosPrimaryKey { get; init; }
        public string CosmosEndPoint { get; init; }

        public string RabbitHost { get; init; }
        public string RabbitUser { get; init; }
        public string RabbitPassword { get; init; }
        public int RabbitPort { get; init; }
    }
}