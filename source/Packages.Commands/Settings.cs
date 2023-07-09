namespace Packages.Commands
{
    public class Settings
    {
        public const string SectionName = "Settings";

        public string Name { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string VaultToken { get; init; } = string.Empty;

        public string VaultAddress { get; init; } = string.Empty;
    }
}