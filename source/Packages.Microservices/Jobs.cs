using Microsoft.Extensions.Options;
using Packages.Microservices.Data;
using Packages.Microservices.Domain;
using Packages.Microservices.Messages;
using Packages.Microservices.Services;
using System.Text.Json;

namespace Packages.Microservices.Jobs
{
    public static class Jobs<TContext> where TContext : Context
    {
        private static Operator<TContext> _operator = null!;

        public static Operator<TContext> Configure(IOptions<Settings> settings)
        {
            _operator ??= new Operator<TContext>(settings);
            return _operator;
        }
    }

    public interface IJob<TContext> where TContext : Context
    {
        string Description { get; }
    }

    public sealed class Operator<TContext> where TContext : Context
    {
        private readonly IOptions<Settings> _settings = null!;
        private readonly IRepository _repository = null!;
        private readonly Broker<TContext> _broker;
        private readonly string _instance = null!;

        private readonly IList<Type> Jobs = new List<Type>();
        private IList<Subscription> Subscriptions = new List<Subscription>();

        internal Operator(IOptions<Settings> settings)
        {
            _settings = settings;

            _instance = $"[{Guid.NewGuid().ToString()[..5].ToLower()}]";

            _broker = new Broker<TContext>(_settings, _instance);

            _repository = new Cosmos<TContext>(_settings, _broker);

            Subscriptions = new List<Subscription>(_repository.Fetch<Subscription>(subscription => subscription.Active == true, StorableType.Subscriptions)
                                                              .GetAwaiter()
                                                              .GetResult());

            _broker.UpdateBindingSubscription(Subscriptions);
            _broker.MessageReceived += MessageReceived;
        }

        private async void MessageReceived(object? sender, MessageEventArgs argument)
        {
            try
            {
                if (argument.Message is null)
                    return;

                argument.Message.DeliveryTag = argument.DeliveryTag;

                _ = argument.Message.Type switch
                {
                    MessageType.Subscription => await RegisterSubscription(argument.Message),
                    MessageType.Replication => await ApplyReplication(argument.Message),

                    _ => false
                };
            }
            catch (Exception exception)
            {
                _broker.PublishError(new Message()
                {
                    Content = exception.Message,
                });

                _broker.ConfirmDelivery(argument.DeliveryTag);
            }
        }

        public Operator<TContext> Schedule<TJob>() where TJob : IJob<TContext>
        {
            var job = Jobs.FirstOrDefault(find => find.FullName == typeof(TJob).FullName);
            if (job is null)
                Jobs.Add(typeof(TJob));

            return this;
        }

        public Task Start()
        {
            try
            {
                PublishContract();
            }
            catch (Exception exception)
            {
                _broker.PublishError(new Message() { Content = $"Failed to Publish Contract:{exception.Message}" });
            }

            return Task.FromResult(true);
        }

        private async Task<bool> ApplyReplication(Message message)
        {
            if (message.Owner != _settings.Value.SourceReplication)
                return false;

            var replication = JsonSerializer.Deserialize<TContext>(message.Content);
            if (replication == null)
                return false;

            replication.DeliveryTag = message.DeliveryTag;

            await _repository.Save<TContext>(replication);

            return true;
        }

        private async Task<bool> RegisterSubscription(Message message)
        {
            var subscription = JsonSerializer.Deserialize<Subscription>(message.Content);
            if (subscription == null || !Subscription.Validade(subscription))
                return false;

            subscription.DeliveryTag = message.DeliveryTag;

            var exists = await _repository.Fetch<Subscription>(x => x.Subscriber == subscription.Subscriber, StorableType.Subscriptions);
            if (exists.Any())
            {
                Parallel.ForEach(exists, async exist =>
                {
                    await _repository.Save<Subscription>(exist.MarkDeleted());
                });
            }

            await _repository.Save<Subscription>(subscription);

            Subscriptions = new List<Subscription>(await _repository.Fetch<Subscription>(subscription => subscription.Active, StorableType.Subscriptions));

            _broker?.UpdateBindingSubscription(Subscriptions);
            _broker?.ConfirmDelivery(message.DeliveryTag);

            return true;
        }

        private async void PublishContract()
        {
            var contextType = typeof(TContext);

            var contract = new Contract
            {
                Name = _settings.Value.Name,
                Description = _settings.Value.Description,
                LastInstance = _instance,
                Exchange = _broker.ExchangeIncoming,
                StartDate = DateTime.UtcNow,
                Context = contextType.Name
            };

            foreach (var property in contextType.GetProperties())
            {
                if (property.DeclaringType == contextType)
                    contract.Models.Add(property.Name);
            }

            foreach (var subscription in Subscriptions)
                contract.Subscriptions.Add(subscription.Subscriber);

            if (CatalogsServices.Current is not null)
                await CatalogsServices.Current.MicroservicePublish(contract);
        }
    }
}