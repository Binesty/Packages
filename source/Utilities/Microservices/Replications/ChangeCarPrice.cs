using Packages.Commands.Domain;
using System.Linq.Expressions;

namespace Microservices.Replications
{
    public class ChangeCarPrice : IReplicable<Sale>
    {
        public Expression<Func<Sale, bool>> InContexts(Replication replication)
        {
            if (replication is null || replication.Content is null)
                return sale => false;

            Car car = replication.Content.ToObject<Car>();
            if (car is null)
                return sale => false;

            return sale => sale.Cars.Any(find => find.Model == car.Model);
        }
    }
}