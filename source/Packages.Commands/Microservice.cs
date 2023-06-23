namespace Packages.Commands
{
    public static class Microservice<TContext> where TContext : Context
    {
        private static Operator<TContext> _operator = null!;

        public static Operator<TContext> Operator =>
            _operator ??= new Operator<TContext>();

        public static Operator<TContext> Configure(IContract contract) =>
            Operator.Configure(contract);

        public static Operator<TContext> Execute<TCommand>() where TCommand : ICommand<TContext> =>
            Operator.Execute<TCommand>();

        public static Operator<TContext> Apply<TReplicable>() where TReplicable : IReplicable<TContext> =>
           Operator.Apply<TReplicable>();

        public static Task<Operator<TContext>> Start() =>
            Operator.Start();
    }
}