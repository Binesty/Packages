using Packages.Microservices.Messages;
using System.Linq.Expressions;

namespace Packages.Microservices.Domain
{
    public class Propagation : IReceivable
    {
        public string? Id { get; set; }

        public dynamic? Content { get; set; }

        public ulong DeliveryTag { get; set; }
    }

    public interface IPropagable<TContext> where TContext : Context
    {
        Expression<Func<TContext, bool>> InContexts(Propagation propagation);

        TContext Apply(TContext context, Propagation propagation);

        bool CanApply(Propagation propagation);
    }
}