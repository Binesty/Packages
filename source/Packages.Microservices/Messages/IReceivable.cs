namespace Packages.Microservices.Messages
{
    public interface IReceivable
    {
        public ulong DeliveryTag { get; set; }
    }
}