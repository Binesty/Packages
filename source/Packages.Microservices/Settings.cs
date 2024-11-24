﻿namespace Packages.Microservices
{
    public class Settings
    {
        public const string SectionName = "Settings";

        public ushort MaxMessagesProcessingInstance { get; init; } = 20_000;

        public string Name { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;
    }
}