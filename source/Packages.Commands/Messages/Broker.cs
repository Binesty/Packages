using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Packages.Commands
{
    internal class Broker
    {
        private const string header = "microservice";
        private const string errors = "errors";

        private const string exchangeEntryPrefix = "entry";
        private const string exchangeReplicationPrefix = "replications";

        private readonly ISettings _settings;
        private IEnumerable<Subscription> _subscriptions;

        private ConnectionFactory _connectionFactoryEntry = null!;
        private IModel _channelEntry = null!;
        private EventingBasicConsumer _eventingBasicConsumerEntry = null!;

        private ConnectionFactory _connectionFactoryReplication = null!;
        private IModel _channelReplication = null!;

        private ConnectionFactory _connectionFactoryErrors = null!;
        private IModel _channelErrors = null!;

        internal event EventHandler<MessageEventArgs>? MessageReceived;

        public virtual void OnMessageReceived(Message message) =>
            MessageReceived?.Invoke(this, new MessageEventArgs() { Message = message });

        public Broker(ISettings settings, IEnumerable<Subscription> subscriptions)
        {
            _settings = settings;
            _subscriptions = subscriptions;

            CreateErrorsChannel();
            CreateReplicationChannel();
            CreateEntryChannel();
        }

        private void CreateErrorsChannel()
        {
            _connectionFactoryErrors = new()
            {
                HostName = _settings.BrokerSettings.Host,
                UserName = _settings.BrokerSettings.User,
                Password = _settings.BrokerSettings.Password,
                Port = _settings.BrokerSettings.Port
            };

            _channelErrors = _connectionFactoryErrors.CreateConnection()
                                                     .CreateModel();

            _channelErrors.ExchangeDeclareNoWait(errors, ExchangeType.Direct, durable: true, autoDelete: false);
            _channelErrors.QueueDeclareNoWait(errors, durable: true, exclusive: false, autoDelete: false, arguments: null);
            _channelErrors.QueueBindNoWait(errors, errors, string.Empty, arguments: null);
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

            string exchange = $"{_settings.Name}-{exchangeReplicationPrefix}";

            _channelReplication.ExchangeDeclareNoWait(exchange, ExchangeType.Headers, durable: true, autoDelete: false);

            Dictionary<string, object> headers = new();

            foreach (var subscription in _subscriptions)
                headers.Add(header, subscription.Subscriber);

            foreach (var subscription in _subscriptions)
                _channelReplication.QueueBindNoWait(subscription.Subscriber, exchange, _settings.Name, arguments: headers);
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

            string exchange = $"{_settings.Name}-{exchangeEntryPrefix}";
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

        public void PublishError(Message message)
        {
            _channelEntry.BasicPublish(errors, errors, null, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)));
        }

        public void Replicate(Message message)
        {
            string exchange = $"{_settings.Name}-{exchangeReplicationPrefix}";
            var headers = new Dictionary<string, object>
            {
                { header, message.Destination }
            };

            IBasicProperties _basicProperties = _channelErrors.CreateBasicProperties();
            _basicProperties.Persistent = true;
            _basicProperties.Headers = headers;

            _channelEntry.BasicPublish(exchange, message.Destination, _basicProperties, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)));
        }
    }
}