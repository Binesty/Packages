﻿namespace Packages.Commands
{
    public class CommandsOptions
    {
        public const string SectionName = "CommandsOptions";

        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;

        public string CosmosPrimaryKey { get; init; } = string.Empty;
        public string CosmosEndPoint { get; init; } = string.Empty;

        public string RabbitHost { get; init; } = string.Empty;
        public string RabbitUser { get; init; } = string.Empty;
        public string RabbitPassword { get; init; } = string.Empty;
        public int RabbitPort { get; init; }
    }
}