namespace Packages.Commands.Data
{
    public interface IStorable
    {
        string Id { get; set; }

        string Partition { get; }

        StorableStatus StorableStatus { get; set; }

        DateTime CreateDate { get; }
    }
}