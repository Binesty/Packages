using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Packages.Commands
{
    internal class Rabbit
    {
        private const string header = "microservice";
        private const string errors = "errors";

        private const string exchangeEntryPrefix = "entry";
        private const string exchangeReplicationPrefix = "replications";

        private readonly IOptions<Options> _options;
        private IList<Subscription> _subscriptions = new List<Subscription>();

        private ConnectionFactory _connectionFactoryEntry = null!;
        private IModel _channelEntry = null!;
        private EventingBasicConsumer _eventingBasicConsumerEntry = null!;

        private ConnectionFactory _connectionFactoryReplication = null!;
        private IModel _channelReplication = null!;

        private ConnectionFactory _connectionFactoryErrors = null!;
        private IModel _channelErrors = null!;

        internal event EventHandler<MessageEventArgs>? MessageReceived;

        public virtual void OnMessageReceived(Message message, ulong deliveryTag) =>
            MessageReceived?.Invoke(this, new MessageEventArgs() { Message = message, DeliveryTag = deliveryTag });

        public Rabbit(IOptions<Options> options, IList<Subscription> subscriptions)
        {
            _options = options;

            _subscriptions = subscriptions;

            CreateErrorsChannel();
            CreateReplicationChannel();
            CreateEntryChannel();
        }

        internal void UpdateBindingSubscription(IList<Subscription> subscriptions)
        {
            if (subscriptions.Count == 0)
                return;

            _subscriptions.Clear();
            _subscriptions = new List<Subscription>(subscriptions);

            string exchange = $"{_options.Value.Name}-{exchangeReplicationPrefix}";

            foreach (var subscription in _subscriptions)
            {
                _channelReplication.QueueBindNoWait(subscription.Subscriber,
                                                    exchange,
                                                    _options.Value.Name,
                                                    arguments: new Dictionary<string, object>() { { header, subscription.Subscriber } });
            }
        }

        private void CreateErrorsChannel()
        {
            _connectionFactoryErrors = new()
            {
                HostName = _options.Value.RabbitHost,
                UserName = _options.Value.RabbitUser,
                Password = _options.Value.RabbitPassword,
                Port = _options.Value.RabbitPort
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
                HostName = _options.Value.RabbitHost,
                UserName = _options.Value.RabbitUser,
                Password = _options.Value.RabbitPassword,
                Port = _options.Value.RabbitPort
            };

            _channelReplication = _connectionFactoryReplication.CreateConnection()
                                                               .CreateModel();

            string exchange = $"{_options.Value.Name}-{exchangeReplicationPrefix}";

            _channelReplication.ExchangeDeclareNoWait(exchange, ExchangeType.Headers, durable: true, autoDelete: false);

            UpdateBindingSubscription(_subscriptions);
        }

        private void CreateEntryChannel()
        {
            _connectionFactoryEntry = new()
            {
                HostName = _options.Value.RabbitHost,
                UserName = _options.Value.RabbitUser,
                Password = _options.Value.RabbitPassword,
                Port = _options.Value.RabbitPort
            };

            _channelEntry = _connectionFactoryEntry.CreateConnection()
                                              .CreateModel();

            string exchange = $"{_options.Value.Name}-{exchangeEntryPrefix}";
            var headers = new Dictionary<string, object>
            {
                { header, _options.Value.Name }
            };

            _channelEntry.ExchangeDeclareNoWait(exchange, ExchangeType.Headers, durable: true, autoDelete: false);
            _channelEntry.QueueDeclareNoWait(_options.Value.Name, durable: true, exclusive: false, autoDelete: false, arguments: headers);
            _channelEntry.QueueBindNoWait(_options.Value.Name, exchange, _options.Value.Name, arguments: headers);

            _eventingBasicConsumerEntry = new EventingBasicConsumer(_channelEntry);
            _eventingBasicConsumerEntry.Received += (model, content) =>
            {
                var message = JsonSerializer.Deserialize<Message>(Encoding.UTF8.GetString(content.Body.ToArray()));
                if (message != null)
                {
                    OnMessageReceived(message, content.DeliveryTag);
                }
            };

            _channelEntry.BasicConsume(_options.Value.Name, false, _eventingBasicConsumerEntry);
        }

        internal void ConfirmDelivery(ulong deliveryTag)
        {
            _channelEntry.BasicAck(deliveryTag: deliveryTag, multiple: false);
        }

        internal void PublishError(Message message)
        {
            _channelEntry.BasicPublish(errors, errors, null, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)));
        }

        internal void Replicate(Message message)
        {
            string exchange = $"{_options.Value.Name}-{exchangeReplicationPrefix}";
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