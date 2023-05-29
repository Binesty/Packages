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
                HostName = _settings.BrokerSettings.Host,
                UserName = _settings.BrokerSettings.User,
                Password = _settings.BrokerSettings.Password,
                Port = _settings.BrokerSettings.Port
            };
            _channelEntry = _connectionFactoryEntry.CreateConnection()
                                                   .CreateModel();

            string exchange = $"entry-{_settings.Name}";
            var headers = new Dictionary<string, string>
            {
                { "microservice", _settings.Name }
            };

            _basicPropertiesEntry = _channelEntry.CreateBasicProperties();
            _basicPropertiesEntry.Persistent = true;

            _channelEntry.ExchangeDeclareNoWait(exchange, ExchangeType.Headers, durable: true, autoDelete: true);
            _channelEntry.QueueDeclareNoWait(_settings.Name, durable: true, exclusive: false, autoDelete: false, arguments: null);
            _channelEntry.QueueBindNoWait(_settings.Name, exchange, _settings.Name, arguments: null);

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

            _channelEntry.BasicConsume(_settings.Name, false, _eventingBasicConsumerEntry);
        }
    }
}