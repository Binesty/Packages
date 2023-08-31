using Packages.Microservices.Commands;
using System.Linq.Expressions;
using System.Text.Json;

namespace Sample.Commands.Domain.Replications
{
    public class CarEndManufacturing : IReplicable<Sale>
    {
        public Expression<Func<Sale, bool>> InContexts(Replication replication)
        {
            if (replication is null)
                return sale => false;

            Filter filter = JsonSerializer.Deserialize<Filter>(replication?.Content);
            if (filter is null)
                return sale => false;

            return sale => sale.Cars.Any(car => car.Model == filter.Model &&
                                                car.Name == filter.Name);
        }

        public Sale Apply(Sale context, Replication replication)
        {
            if (replication is null)
                return context;

            Filter filter = JsonSerializer.Deserialize<Filter>(replication?.Content);
            if (filter is null)
                return context;

            context.Active = false;

            return context;
        }

        public bool CanApply(Replication replication)
        {
            if (replication is null)
                return false;

            Filter filter = JsonSerializer.Deserialize<Filter>(replication?.Content);
            if (filter is null)
                return false;

            if (filter.Model == "Ford")
                return false;

            return true;
        }

        public record Filter(string Model, string Name);
    }
}