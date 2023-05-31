using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Packages.Commands
{
    internal class Broker
    {
        private const string header = "microservice";

        private readonly ISettings _settings;

        private ConnectionFactory _connectionFactoryEntry = null!;
        private IModel _channelEntry = null!;
        private EventingBasicConsumer _eventingBasicConsumerEntry = null!;

        private ConnectionFactory _connectionFactoryReplication = null!;
        private IModel _channelReplication = null!;

        internal event EventHandler<MessageEventArgs>? MessageReceived;

        public virtual void OnMessageReceived(Message message) =>
            MessageReceived?.Invoke(this, new MessageEventArgs() { Message = message });

        public Broker(ISettings settings)
        {
            _settings = settings;

            CreateReplicationChannel();
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
            var headers = new Dictionary<string, object>
            {
                { header, _settings.Name }
            };

            _channelEntry.ExchangeDeclareNoWait(exchange, ExchangeType.Headers, durable: true, autoDelete: false);
            _channelEntry.QueueDeclareNoWait(_settings.Name, durable: true, exclusive: false, autoDelete: false, arguments: headers);
            _channelEntry.QueueBindNoWait(_settings.Name, exchange, _settings.Name, arguments: headers);

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

        private void CreateReplicationChannel()
        {
            _connectionFactoryReplication = new()
            {
                HostName = _settings.BrokerSettings.Host,
                UserName = _settings.BrokerSettings.User,
                Password = _settings.BrokerSettings.Password,
                Port = _settings.BrokerSettings.Port
            };

            _channelReplication = _connectionFactoryReplication.CreateConnection()
                                                               .CreateModel();

            string exchange = $"replications-{_settings.Name}";

            _channelReplication.ExchangeDeclareNoWait(exchange, ExchangeType.Headers, durable: true, autoDelete: false);

            var headers = new Dictionary<string, object>
            {
                { header, "manufacturing" }
            };

            _channelReplication.QueueDeclareNoWait("manufacturing", durable: true, exclusive: false, autoDelete: false, arguments: headers);
            _channelReplication.QueueBindNoWait("manufacturing", exchange, string.Empty, arguments: headers);
        }
    }
}