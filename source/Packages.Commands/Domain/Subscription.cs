namespace Packages.Commands
{
    public class Subscription : IStorable
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Subscriber { get; set; } = string.Empty;

        public DateTime? Date { get; set; } = null;

        public string Command { get; set; } = string.Empty;

        public bool Active { get; set; } = false;

        public IEnumerable<string> Fields { get; set; } = Enumerable.Empty<string>();

        public string Partition => Subscriber;

        public DateTime CreateDate => DateTime.UtcNow;

        public StorableStatus StorableStatus { get; set; }

        StorableType IStorable.StorableType { get; set; } = StorableType.Subscriptions;
    }
}