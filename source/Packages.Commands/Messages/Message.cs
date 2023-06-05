using RabbitMQ.Client;

namespace Packages.Commands
{
    public class Message
    {
        public string Id { get; set; } = string.Empty;

        public MessageType Type { get; set; } = MessageType.None;

        public string Owner { get; set; } = string.Empty;

        public string Destination { get; set; } = string.Empty;

        public string Operation { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public string? Notes { get; set; }

        public DateTime Date { get; set; }
    }

    public enum MessageType : int
    {
        None = 0,
        Command,
        Replication,
        Subscription
    }

    public class MessageEventArgs : EventArgs
    {
        public Message? Message { get; init; }

        public ulong DeliveryTag { get; init; }
    }
}