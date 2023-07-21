namespace Packages.Commands
{
    public interface IReceivable
    {
        public ulong DeliveryTag { get; set; }
    }
}