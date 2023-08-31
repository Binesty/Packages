using System.Linq.Expressions;

namespace Packages.Microservices.Commands
{
    public interface IReplicable<TContext> where TContext : Context
    {
        Expression<Func<TContext, bool>> InContexts(Replication replication);

        TContext Apply(TContext context, Replication replication);

        bool CanApply(Replication replication);
    }
}