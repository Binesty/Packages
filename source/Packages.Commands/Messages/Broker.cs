﻿using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Dynamic;
using System.Text;
using System.Text.Json;

namespace Packages.Commands
{
    internal class Broker<TContext> where TContext : Context
    {
        private const string header = "microservice";
        private const short requestedHeartbeatSeconds = 10;
        private readonly TimeSpan retryDelay = TimeSpan.FromSeconds(requestedHeartbeatSeconds);

        private const string exchangeErrorPrefix = "errors";
        private const string exchangeEntryPrefix = "entry";
        private const string exchangeReplicationPrefix = "replications";

        private readonly IOptions<Settings> _settings;
        private readonly Secrets _secrets;
        private List<Subscription> _subscriptions = new();

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

        public Broker(IOptions<Settings> settings, Secrets secrets)
        {
            _settings = settings;
            _secrets = secrets;

            Start();
        }

        public void Start()
        {
            CreateErrorsChannel();
            CreateReplicationChannel();
            CreateEntryChannel();
            UpdateBindingSubscription(_subscriptions);
        }

        public bool Helth()
        {
            if (_channelEntry.IsClosed ||
                _channelErrors.IsClosed ||
                _channelReplication.IsClosed)
                return false;

            return true;
        }

        internal void UpdateBindingSubscription(IList<Subscription> subscriptions)
        {
            if (subscriptions.Count == 0)
                return;

            _subscriptions = new List<Subscription>(subscriptions);

            string exchange = $"{_settings.Value.Name}-{exchangeReplicationPrefix}";

            foreach (var subscription in _subscriptions)
            {
                _channelReplication.QueueBindNoWait(subscription.Subscriber,
                                                    exchange,
                                                    _settings.Value.Name,
                                                    arguments: new Dictionary<string, object>() { { header, subscription.Subscriber } });
            }
        }

        private void CreateErrorsChannel()
        {
            if (_connectionFactoryErrors is not null)
            {
                if (_channelErrors.IsOpen)
                    _channelErrors.Close();
            }

            _connectionFactoryErrors = new()
            {
                HostName = _secrets.RabbitHost,
                UserName = _secrets.RabbitUser,
                Password = _secrets.RabbitPassword,
                Port = _secrets.RabbitPort,
                ClientProvidedName = $"{_settings.Value.Name}-{exchangeErrorPrefix}",
                RequestedHeartbeat = retryDelay,
                NetworkRecoveryInterval = retryDelay,
                AutomaticRecoveryEnabled = true
            };

            _channelErrors = _connectionFactoryErrors.CreateConnection()
                                                     .CreateModel();

            _channelErrors.ExchangeDeclareNoWait(exchangeErrorPrefix, ExchangeType.Direct, durable: true, autoDelete: false);
            _channelErrors.QueueDeclareNoWait(exchangeErrorPrefix, durable: true, exclusive: false, autoDelete: false, arguments: null);
            _channelErrors.QueueBindNoWait(exchangeErrorPrefix, exchangeErrorPrefix, string.Empty, arguments: null);
        }

        private void CreateReplicationChannel()
        {
            if (_connectionFactoryReplication is not null)
            {
                if (_channelReplication.IsOpen)
                    _channelReplication.Close();
            }

            _connectionFactoryReplication = new()
            {
                HostName = _secrets.RabbitHost,
                UserName = _secrets.RabbitUser,
                Password = _secrets.RabbitPassword,
                Port = _secrets.RabbitPort,
                ClientProvidedName = $"{_settings.Value.Name}-{exchangeReplicationPrefix}",
                RequestedHeartbeat = retryDelay,
                NetworkRecoveryInterval = retryDelay,
                AutomaticRecoveryEnabled = true
            };

            _channelReplication = _connectionFactoryReplication.CreateConnection()
                                                               .CreateModel();

            string exchange = $"{_settings.Value.Name}-{exchangeReplicationPrefix}";

            _channelReplication.ExchangeDeclareNoWait(exchange, ExchangeType.Headers, durable: true, autoDelete: false);

            UpdateBindingSubscription(_subscriptions);
        }

        private void CreateEntryChannel()
        {
            if (_connectionFactoryEntry is not null)
            {
                if (_channelEntry.IsOpen)
                    _channelEntry.Close();
            }

            _connectionFactoryEntry = new()
            {
                HostName = _secrets.RabbitHost,
                UserName = _secrets.RabbitUser,
                Password = _secrets.RabbitPassword,
                Port = _secrets.RabbitPort,
                ClientProvidedName = $"{_settings.Value.Name}-{exchangeEntryPrefix}",
                RequestedHeartbeat = retryDelay,
                NetworkRecoveryInterval = retryDelay,
                AutomaticRecoveryEnabled = true
            };

            _channelEntry = _connectionFactoryEntry.CreateConnection()
                                                   .CreateModel();

            string exchange = $"{_settings.Value.Name}-{exchangeEntryPrefix}";
            var headers = new Dictionary<string, object>
            {
                { header, _settings.Value.Name }
            };

            _channelEntry.ExchangeDeclareNoWait(exchange, ExchangeType.Headers, durable: true, autoDelete: false);
            _channelEntry.QueueDeclareNoWait(_settings.Value.Name, durable: true, exclusive: false, autoDelete: false, arguments: headers);
            _channelEntry.QueueBindNoWait(_settings.Value.Name, exchange, _settings.Value.Name, arguments: headers);
            _channelEntry.BasicQos(prefetchSize: 0, prefetchCount: _settings.Value.MaxMessagesProcessingInstance, global: false);

            _eventingBasicConsumerEntry = new EventingBasicConsumer(_channelEntry);
            _eventingBasicConsumerEntry.Received += (model, content) =>
            {
                var message = JsonSerializer.Deserialize<Message>(Encoding.UTF8.GetString(content.Body.ToArray()));
                if (message != null)
                {
                    OnMessageReceived(message, content.DeliveryTag);
                }
            };

            _channelEntry.BasicConsume(_settings.Value.Name, false, _eventingBasicConsumerEntry);
        }

        internal void ConfirmDelivery(ulong deliveryTag)
        {
            _channelEntry.BasicAck(deliveryTag, false);
        }

        internal void RejectDelivery(ulong deliveryTag)
        {
            _channelEntry.BasicNack(deliveryTag, false, true);
        }

        internal void PublishError(Message message)
        {
            _channelEntry.BasicPublish(exchangeErrorPrefix, exchangeErrorPrefix, null, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)));
        }

        internal void SendReplications(TContext context)
        {
            if (context is null)
                return;

            Parallel.ForEach(_subscriptions, subscription =>
            {
                var replicaton = FilterFieldsContext(context, subscription);

                Message message = new()
                {
                    Id = Guid.NewGuid().ToString(),
                    Owner = _settings.Value.Name,
                    Type = MessageType.Replication,
                    Destination = subscription.Subscriber,
                    Operation = nameof(Replication),
                    Date = DateTime.UtcNow,
                    Content = JsonSerializer.Serialize(replicaton)
                };

                Replicate(message);
            });
        }

        private void Replicate(Message message)
        {
            string exchange = $"{_settings.Value.Name}-{exchangeReplicationPrefix}";
            var headers = new Dictionary<string, object>
            {
                { header, message.Destination }
            };

            IBasicProperties _basicProperties = _channelErrors.CreateBasicProperties();
            _basicProperties.Persistent = true;
            _basicProperties.Headers = headers;

            _channelEntry.BasicPublish(exchange, message.Destination, _basicProperties, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)));
        }

        private static dynamic FilterFieldsContext(TContext context, Subscription subscription)
        {
            var replication = new ExpandoObject();

            foreach (var field in subscription.Fields)
            {
                var property = context.GetType()
                                      .GetProperty(field);

                if (property is null)
                    continue;

                replication.TryAdd(field, property.GetValue(context));
            }

            return replication;
        }
    }
}