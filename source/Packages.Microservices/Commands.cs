﻿using Microsoft.Extensions.Options;
using Packages.Microservices.Data;
using Packages.Microservices.Domain;
using Packages.Microservices.Messages;
using Packages.Microservices.Services;
using System.Linq.Expressions;
using System.Text.Json;

namespace Packages.Microservices.Commands
{
    public static class Commands<TContext> where TContext : Context
    {
        private static Operator<TContext> _operator = null!;

        public static Operator<TContext> Configure(IOptions<Settings> settings)
        {
            _operator ??= new Operator<TContext>(settings);
            return _operator;
        }
    }

    public interface ICommand<TContext> where TContext : Context
    {
        string Description { get; }

        TContext? Execute(TContext context);

        bool CanExecute(TContext context);
    }

    public sealed class Operator<TContext> where TContext : Context
    {
        private readonly IOptions<Settings> _settings = null!;
        private readonly IRepository _repository = null!;
        private readonly Broker<TContext> _broker;
        private readonly string _instance = null!;

        private readonly IList<Type> Commands = new List<Type>();
        private readonly IList<Type> Propagations = new List<Type>();
        private IList<Subscription> Subscriptions = new List<Subscription>();
        private readonly Queue<TContext> _queueContextsCommands = new();
        private readonly PeriodicTimer periodicTimer = new(TimeSpan.FromSeconds(1));

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
                    MessageType.Command => ExecuteCommand(argument.Message),
                    MessageType.Subscription => await RegisterSubscription(argument.Message),
                    MessageType.Propagation => await ApplyPropagation(argument.Message),

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

        public Operator<TContext> Execute<TCommand>() where TCommand : ICommand<TContext>
        {
            var command = Commands.FirstOrDefault(find => find.FullName == typeof(TCommand).FullName);
            if (command is null)
                Commands.Add(typeof(TCommand));

            return this;
        }

        public Operator<TContext> Apply<TReplicable>() where TReplicable : IPropagable<TContext>
        {
            var propagation = Propagations.FirstOrDefault(find => find.FullName == typeof(TReplicable).FullName);
            if (propagation is null)
                Propagations.Add(typeof(TReplicable));

            return this;
        }

        public async Task Start()
        {
            try
            {
                PublishContract();
            }
            catch (Exception exception)
            {
                _broker.PublishError(new Message() { Content = $"Failed to Publish Contract:{exception.Message}" });
            }

            while (await periodicTimer.WaitForNextTickAsync())
            {
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
                    lock (_queueContextsCommands)
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

        private async Task<bool> ApplyPropagation(Message message)
        {
            var propagation = JsonSerializer.Deserialize<Propagation>(message.Content);
            if (propagation == null)
                return false;

            propagation.DeliveryTag = message.DeliveryTag;

            var propagationType = Propagations.FirstOrDefault(item => item.Name == message.Operation);
            if (propagationType is null)
                return false;

            var propagate = Activator.CreateInstance(propagationType);
            if (propagate is null)
                return false;

            var queryImplemented = ((IPropagable<TContext>)propagate).InContexts(propagation);
            Expression<Func<TContext, bool>> filterIdPropagations = context => context.LastPropagationId != propagation.Id;

            var contexts = await _repository.Fetch(queryImplemented, StorableType.Contexts, filterIdPropagations);
            if (contexts.Any())
            {
                List<TContext> contextsToPropagate = new();
                Parallel.ForEach(contexts, context =>
                {
                    if (!((IPropagable<TContext>)propagate).CanApply(propagation))
                        return;

                    var change = ((IPropagable<TContext>)propagate).Apply(context, propagation);
                    if (change is null)
                        return;

                    change.StorableStatus = StorableStatus.Changed;
                    change.LastPropagationId = propagation.Id;
                    change.LastOperation = propagationType.Name;

                    contextsToPropagate.Add(change);
                });

                if (contextsToPropagate.Any())
                    await _repository.BulkSave<TContext>(contextsToPropagate);
            }

            _broker.ConfirmDelivery(message.DeliveryTag);
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
                contextChange.Name = commandType.Name;
                contextChange.LastOperation = commandType.Name;
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

            foreach (var propagation in Propagations)
                contract.Propagations.Add(propagation.Name);

            foreach (var subscription in Subscriptions)
                contract.Subscriptions.Add(subscription.Subscriber);

            if (CatalogsServices.Current is not null)
                await CatalogsServices.Current.MicroservicePublish(contract);
        }
    }
}