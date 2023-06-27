namespace Packages.Commands
{
    public class Options
    {
        public const string SectionName = "Options";

        public required string Name { get; init; }
        public required string Description { get; init; }
        
        public required string CosmosPrimaryKey { get; init; }
        public required string CosmosEndPoint { get; init; }

        public required string RabbitHost { get; init; }
        public required string RabbitUser { get; init; }
        public required string RabbitPassword { get; init; }
        public required int RabbitPort { get; init; }
    }
}