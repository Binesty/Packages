using Packages.Microservices.Domain;
using System.Linq.Expressions;
using System.Text.Json;

namespace Sample.Commands.Propagations
{
    public class CarEndManufacturing : IPropagable<Sale>
    {
        public Expression<Func<Sale, bool>> InContexts(Propagation propagation)
        {
            if (propagation is null)
                return sale => false;

            Filter filter = JsonSerializer.Deserialize<Filter>(propagation?.Content);
            if (filter is null)
                return sale => false;

            return sale => sale.Cars.Any(car => car.Model == filter.Model &&
                                                car.Name == filter.Name);
        }

        public Sale Propagate(Sale context, Propagation propagation)
        {
            if (propagation is null)
                return context;

            Filter filter = JsonSerializer.Deserialize<Filter>(propagation?.Content);
            if (filter is null)
                return context;

            context.Active = false;

            return context;
        }

        public bool CanPropagate(Propagation propagation)
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