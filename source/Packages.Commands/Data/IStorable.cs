namespace Packages.Commands
{
    public interface IStorable : IReceivable
    {
        string Id { get; set; }

        string Partition { get; }

        StorableStatus StorableStatus { get; set; }

        StorableType StorableType { get; set; }

        DateTime CreateDate { get; }
    }

    public enum StorableStatus
    {
        New,
        NotChanged,
        Changed,
        Deleted
    }

    public enum StorableType
    {
        Contexts,
        Subscriptions
    }
}