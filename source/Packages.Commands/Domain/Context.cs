namespace Packages.Commands
{
    public abstract class Context : IStorable
    {
        public abstract string Name { get; }

        public abstract string Description { get; }

        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Partition => Name;

        public DateTime CreateDate => DateTime.UtcNow;

        public StorableStatus StorableStatus { get; set; }

        StorableType IStorable.StorableType { get; set; } = StorableType.Contexts;

        public string? LastReplicationId { get; set; }
    }
}