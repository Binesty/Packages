using Packages.Commands.Data;
using System.Text.Json;

namespace Packages.Commands
{
    public sealed class Operator<TContext> where TContext : Context
    {
        private ISettings? _settings;
        private Broker? _broker;
        private IRepository _repository = null!;
        private readonly IList<Type> Commands = new List<Type>();

        internal Operator<TContext> Configure(ISettings settings)
        {
            _settings = settings;

            _broker = new(_settings);
            _repository = new Cosmos<TContext>(_settings);
            _broker.MessageReceived += MessageReceived;

            return this;
        }

        public Operator<TContext> Execute<TCommand>() where TCommand : ICommand<TContext>
        {
            var command = Commands.FirstOrDefault(find => find.FullName == typeof(TCommand).FullName);
            if (command is null)
                Commands.Add(typeof(TCommand));

            return this;
        }

        public Operator<TContext> Start()
        {
            return this;
        }

        private void MessageReceived(object? sender, MessageEventArgs argument)
        {
            if (argument.Message is null)
                return;

            _ = argument.Message.Type switch
            {
                MessageType.Command => ExecuteCommand(argument.Message).GetAwaiter()
                                                                       .GetResult(),
                _ => false
            };
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

            var change = ((ICommand<TContext>)command).Execute(context);
            if (change is null)
                return false;

            await _repository.Save<TContext>(change);

            return true;
        }
    }
}