using Packages.Microservices.Data;
using Packages.Microservices.Messages;

namespace Packages.Microservices.Domain
{
    public abstract class Context : IStorable, IReceivable
    {
        public string Name { get; set; } = string.Empty;

        public string Id { get; set; } = Guid.NewGuid().ToString();

        public DateTime CreateDate => DateTime.UtcNow;

        public StorableStatus StorableStatus { get; set; }

        public string? LastOperation { get; set; }

        public string? LastReplicationId { get; set; }

        public ulong DeliveryTag { get; set; }

        public StorableType StorableType { get; set; } = StorableType.Contexts;
        public string Partition => Name;
    }
}