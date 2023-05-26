using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Packages.Commands
{
    internal class Broker
    {
        private readonly ISettings _settings;

        private IBasicProperties _basicPropertiesEntry = null!;
        private IModel _channelEntry = null!;
        private ConnectionFactory _connectionFactoryEntry = null!;
        private EventingBasicConsumer _eventingBasicConsumerEntry = null!;

        internal event EventHandler<MessageEventArgs>? MessageReceived;
        public virtual void OnMessageReceived(Message message) =>
            MessageReceived?.Invoke(this, new MessageEventArgs() { Message = message });

        public Broker(ISettings settings)
        {
            _settings = settings;

            CreateEntryChannel();
        }

        private void CreateEntryChannel()
        {
            _connectionFactoryEntry = new()
            {
                HostName = _settings.Infrastructure.BrokerHost,
                UserName = _settings.Infrastructure.BrokerUser,
                Password = _settings.Infrastructure.BrokerPassword,
                Port = _settings.Infrastructure.BrokerPort
            };
            _channelEntry = _connectionFactoryEntry.CreateConnection()
                                                   .CreateModel();

            _basicPropertiesEntry = _channelEntry.CreateBasicProperties();
            _basicPropertiesEntry.Persistent = true;

            _channelEntry.ExchangeDeclareNoWait(_settings.Name, "topic", true, false);
            _channelEntry.QueueDeclareNoWait(_settings.Name, true, false, false, null);

            _eventingBasicConsumerEntry = new EventingBasicConsumer(_channelEntry);
            _eventingBasicConsumerEntry.Received += (model, content) =>
            {
                _channelEntry.BasicAck(deliveryTag: content.DeliveryTag, multiple: false);

                var message = JsonSerializer.Deserialize<Message>(Encoding.UTF8.GetString(content.Body.ToArray()));
                if (message != null)
                {
                    OnMessageReceived(message);
                }
            };

            _channelEntry.BasicConsume(queue: _settings.Name, autoAck: false, consumer: _eventingBasicConsumerEntry);
        }
    }

    public class MessageEventArgs: EventArgs
    {
        public Message? Message { get; init; }
    }
}
