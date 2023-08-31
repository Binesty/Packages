using Packages.Microservices.Messages;

namespace Packages.Microservices.Commands
{
    public class Replication : IReceivable
    {
        public string? Id { get; set; }

        public dynamic? Content { get; set; }

        public ulong DeliveryTag { get; set; }
    }
}