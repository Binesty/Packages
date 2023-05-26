namespace Packages.Commands
{
    public class Message
    {
        public string Id { get; set; } = string.Empty;

        public string Owner { get; set; } = string.Empty;

        public string Destination { get; set; } = string.Empty;

        public string Operation { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public DateTime Date { get; set; }
    }
}