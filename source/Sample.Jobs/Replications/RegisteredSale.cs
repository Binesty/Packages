using Packages.Microservices.Domain;
using Sample.Jobs;
using System.Linq.Expressions;
using System.Text.Json;

namespace Sample.Commands.Propagations
{
    public class RegisteredSale : IPropagable<Sale>
    {
        public Expression<Func<Sale, bool>> InContexts(Propagation propagation)
        {
            if (propagation is null)
                return result => false;

            Filter filter = JsonSerializer.Deserialize<Filter>(propagation?.Content);
            if (filter is null)
                return sale => false;

            return sale => true;
        }

        public Sale Apply(Sale context, Propagation propagation)
        {
            if (propagation is null)
                return context;

            Filter filter = JsonSerializer.Deserialize<Filter>(propagation?.Content);
            if (filter is null)
                return context;

            context.Active = false;

            return context;
        }

        public bool CanApply(Propagation propagation)
        {
            if (propagation is null)
                return false;

            Filter filter = JsonSerializer.Deserialize<Filter>(propagation?.Content);
            if (filter is null)
                return false;

            if (filter.Model == "Ford")
                return false;

            return true;
        }

        public record Filter(string Model, string Name);
    }
}