namespace Packages.Commands
{
    public class Subscription : IStorable, IReceivable
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Subscriber { get; set; } = string.Empty;

        public DateTime? Date { get; set; } = null;

        public string Command { get; set; } = string.Empty;

        public bool Active { get; set; } = false;

        public List<string> Fields { get; set; } = new();

        public string Partition => Subscriber;

        public DateTime CreateDate => DateTime.UtcNow;

        public StorableStatus StorableStatus { get; set; }

        public ulong DeliveryTag { get; set; }

        StorableType IStorable.StorableType { get; set; } = StorableType.Subscriptions;

        internal static bool Validade(Subscription subscription)
        {
            if (subscription.Fields is null || subscription.Fields.Count == 0)
                return false;

            if (string.IsNullOrEmpty(subscription.Subscriber))
                return false;

            if (string.IsNullOrEmpty(subscription.Command))
                return false;

            return true;
        }

        public Subscription MarkDeleted()
        {
            StorableStatus = StorableStatus.Deleted;
            return this;
        }
    }
}