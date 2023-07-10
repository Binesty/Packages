using Microsoft.Extensions.Options;

namespace Packages.Commands
{
    public static class Microservice<TContext> where TContext : Context
    {
        private static Operator<TContext> _operator = null!;

        public static Operator<TContext> Configure(IOptions<Settings> settings)
        {
            _operator ??= new Operator<TContext>(settings);
            return _operator;
        }

        public static Operator<TContext> Execute<TCommand>() where TCommand : ICommand<TContext> =>
            _operator.Execute<TCommand>();

        public static Operator<TContext> Apply<TReplicable>() where TReplicable : IReplicable<TContext> =>
           _operator.Apply<TReplicable>();
    }
}