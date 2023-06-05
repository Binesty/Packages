using Packages.Commands;
using System.Linq.Expressions;
using System.Text.Json;

namespace Microservices.Replications
{
    public class CarEndManufacturing : IReplicable<Sale>
    {
        public Expression<Func<Sale, bool>> InContexts(Replication replication)
        {
            if (replication is null)
                return sale => false;

            Filter filter = JsonSerializer.Deserialize<Filter>(replication.Content);
            if (filter is null)
                return sale => false;

            return sale => sale.Cars.Any(car => car.Model == filter.Model &&
                                                car.Name  == filter.Name);
        }

        public record Filter(string Model, string Name);
    }
}