namespace Packages.Commands
{
    public sealed class Operator<TContext> where TContext : Context
    {        
        private ISettings? _settings;
        private Broker? _broker;        
        private readonly IList<Type> Commands = new List<Type>();

        internal Operator<TContext> Configure(ISettings settings)
        {
            _settings = settings;

            _broker = new(_settings);
            _broker.MessageReceived += MessageReceived;

            return this;
        }

        private void MessageReceived(object? sender, MessageEventArgs args)
        {
            if (args.Message is null)
                return;

            var message = args.Message;
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
    }
}