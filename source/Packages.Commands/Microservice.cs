namespace Packages.Commands
{
    public static class Microservice<TContext> where TContext : Context
    {
        private static Operator<TContext> _operator = null!;

        public static Operator<TContext> Operator
        {
            get
            {
                _operator ??= new Operator<TContext>();
                return _operator;
            }
        }

        public static Operator<TContext> Configure(ISettings settings) =>
            Operator.Configure(settings);

        public static Operator<TContext> Execute<TCommand>() where TCommand : ICommand<TContext> =>
            Operator.Execute<TCommand>();

        public static Operator<TContext> Start() =>
            Operator.Start();
    }
}