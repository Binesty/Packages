﻿using Microsoft.Extensions.Options;
using Packages.Microservices.Domain;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Dynamic;
using System.Text;
using System.Text.Json;

namespace Packages.Microservices.Messages
{
    internal class Broker<TContext> where TContext : Context
    {
        private const short requestedHeartbeatSeconds = 10;
        private readonly TimeSpan retryDelay = TimeSpan.FromSeconds(requestedHeartbeatSeconds);
        private readonly string _instance = string.Empty;
        private readonly BrokerCommunication.Configuration _brokerCommunicationConfiguration;

        internal string ExchangeEntry => _brokerCommunicationConfiguration.ExchangeEntry;

        private readonly IOptions<Settings> _settings;
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

        public Broker(IOptions<Settings> settings, string instance)
        {
            _settings = settings;
            _instance = instance;
            _brokerCommunicationConfiguration = BrokerCommunication.GetConfiguration(_settings.Value.Name);

            Start();
        }

        public void Start()
        {
            CreateErrorsChannel();
            CreateReplicationChannel();
            CreateEntryChannel();
            UpdateBindingSubscription(_subscriptions);
        }

        internal void UpdateBindingSubscription(IList<Subscription> subscriptions)
        {
            if (subscriptions.Count == 0)
                return;

            _subscriptions = new List<Subscription>(subscriptions);

            foreach (var subscription in _subscriptions)
            {
                _channelReplication.QueueBindNoWait(subscription.Subscriber,
                                                    _brokerCommunicationConfiguration.ExchangeReplication,
                                                    _brokerCommunicationConfiguration.QueueName,
                                                    arguments: new Dictionary<string, object>() { { _brokerCommunicationConfiguration.Header, subscription.Subscriber } });
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
                HostName = Secret.Loaded.RabbitHost,
                UserName = Secret.Loaded.RabbitUser,
                Password = Secret.Loaded.RabbitPassword,
                Port = Secret.Loaded.RabbitPort,
                ClientProvidedName = $"{_instance}-{_brokerCommunicationConfiguration.ExchangeErrors}",
                RequestedHeartbeat = retryDelay,
                NetworkRecoveryInterval = retryDelay,
                AutomaticRecoveryEnabled = true
            };

            _channelErrors = _connectionFactoryErrors.CreateConnection()
                                                     .CreateModel();

            _channelErrors.ExchangeDeclareNoWait(_brokerCommunicationConfiguration.ExchangeErrors,
                                                 ExchangeType.Direct, durable: true, autoDelete: false);

            _channelErrors.QueueDeclareNoWait(_brokerCommunicationConfiguration.ExchangeErrors,
                                              durable: true, exclusive: false, autoDelete: false, arguments: null);

            _channelErrors.QueueBindNoWait(_brokerCommunicationConfiguration.ExchangeErrors,
                                           _brokerCommunicationConfiguration.ExchangeErrors, string.Empty, arguments: null);
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
                HostName = Secret.Loaded.RabbitHost,
                UserName = Secret.Loaded.RabbitUser,
                Password = Secret.Loaded.RabbitPassword,
                Port = Secret.Loaded.RabbitPort,
                ClientProvidedName = $"{_instance}-{_brokerCommunicationConfiguration.ExchangeReplication}",
                RequestedHeartbeat = retryDelay,
                NetworkRecoveryInterval = retryDelay,
                AutomaticRecoveryEnabled = true
            };

            _channelReplication = _connectionFactoryReplication.CreateConnection()
                                                               .CreateModel();

            _channelReplication.ExchangeDeclareNoWait(_brokerCommunicationConfiguration.ExchangeReplication,
                                                      ExchangeType.Headers, durable: true, autoDelete: false);

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
                HostName = Secret.Loaded.RabbitHost,
                UserName = Secret.Loaded.RabbitUser,
                Password = Secret.Loaded.RabbitPassword,
                Port = Secret.Loaded.RabbitPort,
                ClientProvidedName = $"{_instance}-{_brokerCommunicationConfiguration.ExchangeEntry}",
                RequestedHeartbeat = retryDelay,
                NetworkRecoveryInterval = retryDelay,
                AutomaticRecoveryEnabled = true
            };

            _channelEntry = _connectionFactoryEntry.CreateConnection()
                                                   .CreateModel();

            var headers = new Dictionary<string, object>
            {
                { _brokerCommunicationConfiguration.Header, _brokerCommunicationConfiguration.HeaderValue }
            };

            _channelEntry.ExchangeDeclareNoWait(_brokerCommunicationConfiguration.ExchangeEntry,
                                                ExchangeType.Headers, durable: true, autoDelete: false);

            _channelEntry.QueueDeclareNoWait(_brokerCommunicationConfiguration.QueueName,
                                             durable: true, exclusive: false, autoDelete: false, arguments: headers);

            _channelEntry.QueueBindNoWait(_brokerCommunicationConfiguration.QueueName,
                                          _brokerCommunicationConfiguration.ExchangeEntry,
                                          _brokerCommunicationConfiguration.QueueName, arguments: headers);

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

            _channelEntry.BasicConsume(_brokerCommunicationConfiguration.QueueName, false, _eventingBasicConsumerEntry);
        }

        internal void ConfirmDelivery(ulong deliveryTag)
        {
            if (_channelEntry.IsClosed)
                CreateEntryChannel();

            _channelEntry.BasicAck(deliveryTag, false);
        }

        internal void RejectDelivery(ulong deliveryTag)
        {
            if (_channelEntry.IsClosed)
                CreateEntryChannel();

            _channelEntry.BasicNack(deliveryTag, false, true);
        }

        internal void PublishError(Message message)
        {
            if (_channelErrors.IsClosed)
                CreateErrorsChannel();

            _channelEntry.BasicPublish(_brokerCommunicationConfiguration.ExchangeErrors,
                                       _brokerCommunicationConfiguration.ExchangeErrors,
                                       null, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)));
        }

        internal void SendReplications(TContext context)
        {
            if (_channelReplication.IsClosed)
                CreateReplicationChannel();

            if (context is null)
                return;

            Parallel.ForEach(_subscriptions.FindAll(find => find.Operataion == context.LastOperation), subscription =>
            {
                var replicaton = FilterFieldsContext(context, subscription);

                Message message = new()
                {
                    Id = Guid.NewGuid().ToString(),
                    Owner = _brokerCommunicationConfiguration.QueueName,
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
            var headers = new Dictionary<string, object>
            {
                { _brokerCommunicationConfiguration.Header, message.Destination }
            };

            IBasicProperties _basicProperties = _channelErrors.CreateBasicProperties();
            _basicProperties.Persistent = true;
            _basicProperties.Headers = headers;

            _channelEntry.BasicPublish(_brokerCommunicationConfiguration.ExchangeReplication,
                                       message.Destination, _basicProperties,
                                       Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)));
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

            replication.TryAdd(nameof(Replication.Id), Guid.NewGuid().ToString());
            return replication;
        }
    }

    public static class BrokerCommunication
    {
        public static Configuration GetConfiguration(string microservice)
        {
            const string type = "commands";
            return new Configuration
            {
                Header = "microservice",
                HeaderValue = microservice,
                ExchangeEntry = $"{type}:{microservice}:entry",
                ExchangeReplication = $"{type}:{microservice}:replications",
                ExchangeErrors = $"{type}:errors",
                QueueName = $"{type}:{microservice}",
                QueueErrors = $"{type}:errors"
            };
        }

        public struct Configuration
        {
            public string Header { get; set; }

            public string HeaderValue { get; set; }

            public string ExchangeEntry { get; set; }
            public string ExchangeReplication { get; set; }

            public string ExchangeErrors { get; set; }

            public string QueueName { get; set; }
            public string QueueErrors { get; set; }
        }
    }
}