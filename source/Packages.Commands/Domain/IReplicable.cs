using System.Linq.Expressions;

namespace Packages.Commands
{
    public interface IReplicable<TContext> where TContext : Context
    {
        Expression<Func<TContext, bool>> InContexts(Replication replication);
    }
}