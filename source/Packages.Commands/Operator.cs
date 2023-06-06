using System.Dynamic;
using System.Text.Json;

namespace Packages.Commands
{
    public sealed class Operator<TContext> where TContext : Context
    {
        private ISettings _settings = null!;
        private Broker? _broker;
        private IRepository _repository = null!;
        private readonly IList<Type> Commands = new List<Type>();
        private readonly IList<Type> Replications = new List<Type>();
        private IList<Subscription> Subscriptions = new List<Subscription>();

        internal Operator<TContext> Configure(ISettings settings)
        {
            _settings = settings;
            _repository = new Cosmos<TContext>(_settings);

            return this;
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

        public async Task<Operator<TContext>> Start()
        {
            Subscriptions = new List<Subscription>(await _repository.Fetch<Subscription>(subscription => subscription.Active, StorableType.Subscriptions));

            _broker = new(_settings, Subscriptions);
            _broker.MessageReceived += MessageReceived;

            return this;
        }

        private void MessageReceived(object? sender, MessageEventArgs argument)
        {
            if (argument.Message is null)
                return;

            Task.Run(async () =>
            {
                _ = argument.Message.Type switch
                {
                    MessageType.Command => await ExecuteCommand(argument.Message),
                    MessageType.Subscription => await RegisterSubscription(argument.Message),
                    MessageType.Replication => await ApplyReplication(argument.Message),

                    _ => false
                };

                _broker?.ConfirmDelivery(argument.DeliveryTag);
            }).ContinueWith(continuetion =>
            {
                argument.Message.Notes = continuetion.Exception?.Message;
                _broker?.PublishError(argument.Message);
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private async Task<bool> ApplyReplication(Message message)
        {
            var replication = JsonSerializer.Deserialize<Replication>(message.Content);
            if (replication == null)
                return false;

            var replicationType = Replications.FirstOrDefault(item => item.Name == message.Operation);
            if (replicationType is null)
                return false;

            var replicate = Activator.CreateInstance(replicationType);
            if (replicate is null)
                return false;

            var contexts = await _repository.Fetch(((IReplicable<TContext>)replicate).InContexts(replication), StorableType.Contexts);

            Parallel.ForEach(contexts, async context =>
            {
                if (!((IReplicable<TContext>)replicate).CanApply(replication))
                    return;

                var change = ((IReplicable<TContext>)replicate).Apply(context, replication);
                if (change is null)
                    return;

                context.StorableStatus = StorableStatus.Changed;
                await _repository.Save<TContext>(change);

                SendReplications(change);
            });

            return true;
        }

        private async Task<bool> ExecuteCommand(Message message)
        {
            var context = JsonSerializer.Deserialize<TContext>(message.Content);
            if (context == null)
                return false;

            var commandType = Commands.FirstOrDefault(item => item.Name == message.Operation);
            if (commandType is null)
                return false;

            var command = Activator.CreateInstance(commandType);
            if (command is null)
                return false;

            if (!((ICommand<TContext>)command).CanExecute(context))
                return false;

            var change = ((ICommand<TContext>)command).Execute(context);
            if (change is null)
                return false;

            await _repository.Save<TContext>(change);

            SendReplications(change);

            return true;
        }

        private async Task<bool> RegisterSubscription(Message message)
        {
            var subscription = JsonSerializer.Deserialize<Subscription>(message.Content);
            if (subscription == null || !Subscription.Validade(subscription))
                return false;

            await _repository.Save<Subscription>(subscription);

            Subscriptions = new List<Subscription>(await _repository.Fetch<Subscription>(subscription => subscription.Active, StorableType.Subscriptions));

            _broker?.UpdateBindingSubscription(Subscriptions);

            return true;
        }

        private void SendReplications(TContext context)
        {
            Parallel.ForEach(Subscriptions, subscription =>
            {
                var replicaton = Operator<TContext>.FilterFieldsContext(context, subscription);

                Message message = new()
                {
                    Id = Guid.NewGuid().ToString(),
                    Owner = _settings.Name,
                    Type = MessageType.Replication,
                    Destination = subscription.Subscriber,
                    Operation = nameof(Replication),
                    Date = DateTime.UtcNow,
                    Content = JsonSerializer.Serialize(replicaton)
                };

                _broker?.Replicate(message);
            });
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