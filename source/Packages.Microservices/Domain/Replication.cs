using Packages.Microservices.Messages;

namespace Packages.Microservices.Domain
{
    public class Replication : IReceivable
    {
        public string? Id { get; set; }

        public dynamic? Content { get; set; }

        public ulong DeliveryTag { get; set; }
    }

    public interface IReplicable<TContext> where TContext : Context
    {
        TContext? Replicate(Replication replication);
    }
}
