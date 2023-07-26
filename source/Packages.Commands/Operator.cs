using Microsoft.Extensions.Options;
using System.Linq.Expressions;
using System.Text.Json;

namespace Packages.Commands
{
    public sealed class Operator<TContext> where TContext : Context
    {
        private readonly IOptions<Settings> _settings = null!;
        private readonly IRepository _repository = null!;
        private readonly Broker<TContext> _broker;
        private readonly Secrets _secrets = null!;           
        private readonly string _instance = null!;

        private readonly IList<Type> Commands = new List<Type>();
        private readonly IList<Type> Replications = new List<Type>();
        private IList<Subscription> Subscriptions = new List<Subscription>();
        private readonly Queue<TContext> _queueContextsCommands = new();
        private readonly PeriodicTimer periodicTimer = new(TimeSpan.FromSeconds(1));

        internal Operator(IOptions<Settings> settings)
        {
            _settings = settings;
            _secrets = Secrets.Load(_settings);
            _instance = $"[{Guid.NewGuid().ToString()[..5].ToLower()}]";

            _broker = new Broker<TContext>(_settings, _secrets, _instance);

            _repository = new Cosmos<TContext>(_settings, _secrets, _broker);

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

                var processed = argument.Message.Type switch
                {
                    MessageType.Command => ExecuteCommand(argument.Message),
                    MessageType.Subscription => await RegisterSubscription(argument.Message),
                    MessageType.Replication => await ApplyReplication(argument.Message),

                    _ => false
                };

                if (!processed)
                {
                    _broker.PublishError(new Message()
                    {
                        Content = $"Error Processed: {JsonSerializer.Serialize(argument.Message)}",
                    });

                    _broker.RejectDelivery(argument.DeliveryTag);
                }
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

        public Operator<TContext> Execute<TCommand>() where TCommand : ICommand<TContext>
        {
            var command = Commands.FirstOrDefault(find => find.FullName == typeof(TCommand).FullName);
            if (command is null)
                Commands.Add(typeof(TCommand));

            return this;
        }

        public Operator<TContext> Apply<TReplicable>() where TReplicable : IReplicable<TContext>
        {
            var replication = Replications.FirstOrDefault(find => find.FullName == typeof(TReplicable).FullName);
            if (replication is null)
                Replications.Add(typeof(TReplicable));

            return this;
        }

        public async Task Start()
        {
            PublishContract();

            while (await periodicTimer.WaitForNextTickAsync())
            {
                if (!_broker.Helth())
                    _broker.Start();

                var contexts = GetContexts();
                if (contexts.Any())
                    await _repository.BulkSave<TContext>(contexts);
            }
        }

        private IEnumerable<TContext> GetContexts()
        {
            List<TContext> contexts = new();
            for (int i = 0; i < _settings.Value.MaxMessagesProcessingInstance; i++)
            {
                if (_queueContextsCommands.Any())
                {
                    lock(_queueContextsCommands)
                    {
                        var context = _queueContextsCommands.Dequeue();
                        if (context is null)
                            continue;

                        contexts.Add(context);
                    }
                }
            }

            return contexts;
        }

        private async Task<bool> ApplyReplication(Message message)
        {
            var replication = JsonSerializer.Deserialize<Replication>(message.Content);
            if (replication == null)
                return false;

            replication.DeliveryTag = message.DeliveryTag;

            var replicationType = Replications.FirstOrDefault(item => item.Name == message.Operation);
            if (replicationType is null)
                return false;

            var replicate = Activator.CreateInstance(replicationType);
            if (replicate is null)
                return false;

            var queryImplemented = ((IReplicable<TContext>)replicate).InContexts(replication);
            Expression<Func<TContext, bool>> queryFilterReplicationId = context => context.LastReplicationId != replication.Id;

            var contexts = await _repository.Fetch(queryImplemented, StorableType.Contexts, queryFilterReplicationId);

            Parallel.ForEach(contexts, async context =>
            {
                if (!((IReplicable<TContext>)replicate).CanApply(replication))
                    return;

                var change = ((IReplicable<TContext>)replicate).Apply(context, replication);
                if (change is null)
                    return;

                context.StorableStatus = StorableStatus.Changed;
                context.LastReplicationId = replication.Id;

                await _repository.Save<TContext>(change);

                _broker.SendReplications(change);
            });

            return true;
        }

        private bool ExecuteCommand(Message message)
        {
            var context = JsonSerializer.Deserialize<TContext>(message.Content);
            if (context == null)
                return false;

            context.DeliveryTag = message.DeliveryTag;

            var commandType = Commands.FirstOrDefault(item => item.Name == message.Operation);
            if (commandType is null)
                return false;

            var command = Activator.CreateInstance(commandType);
            if (command is null)
                return false;

            if (!((ICommand<TContext>)command).CanExecute(context))
                return false;

            var contextChange = ((ICommand<TContext>)command).Execute(context);
            if (contextChange is null)
                return false;

            lock (_queueContextsCommands)
            {
                _queueContextsCommands.Enqueue(contextChange);
            }

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

            return true;
        }

        private void PublishContract()
        {
            var contextType = typeof(TContext);

            var contract = new Contract
            {
                Name = _settings.Value.Name,
                Description = _settings.Value.Description,
                LastInstance = _instance,
                Exchange = _broker.ExchangeEntry,
                StartDate = DateTime.UtcNow,
                Context = contextType.Name
            };

            foreach (var property in contextType.GetProperties())
            {
                if (property.DeclaringType == contextType)
                    contract.Models.Add(property.Name);
            }
            
            foreach (var command in Commands)
                contract.Commands.Add(command.Name);

            foreach (var replication in Replications)
                contract.Replications.Add(replication.Name);

            foreach (var subscription in Subscriptions)
                contract.Subscriptions.Add(subscription.Subscriber);


        }
    }
}