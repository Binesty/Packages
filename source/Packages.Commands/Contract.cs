namespace Packages.Commands
{
    public class Contract
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string LastInstance { get; set; } = string.Empty;

        public string Exchange { get; set; } = string.Empty;

        public DateTime? StartDate { get; set; }

        public string Context { get; set; } = string.Empty;

        public List<string> Models { get; set; } = new();

        public List<string> Commands { get; set; } = new();

        public List<string> Replications { get; set; } = new();

        public List<string> Subscriptions { get; set; } = new();
    }
}