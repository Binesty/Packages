using Packages.Microservices.Messages;

namespace Packages.Microservices.Domain
{
    public class Propagation : IReceivable
    {
        public string? Id { get; set; }

        public dynamic? Content { get; set; }

        public ulong DeliveryTag { get; set; }
    }
}