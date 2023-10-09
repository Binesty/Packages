using System.Linq.Expressions;

namespace Packages.Microservices.Domain
{
    public interface IPropagable<TContext> where TContext : Context
    {
        Expression<Func<TContext, bool>> InContexts(Propagation propagation);

        TContext Apply(TContext context, Propagation propagation);

        bool CanApply(Propagation propagation);
    }
}