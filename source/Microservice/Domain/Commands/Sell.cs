using Packages.Microservices.Domain;

namespace Microservice.Domain.Commands
{
    public class Sell : ICommand<Sale>
    {
        public string Description => "Command that carries out the sale of cars";

        public Sale? Execute(Sale context)
        {
            context.Date = DateTime.Now;

            context.Price = context.Cars.Sum(car => car.Price);

            return context;
        }

        public bool CanExecute(Sale context)
        {
            if (context == null)
                return false;

            if (context.Customer is null)
                return false;

            if (context.Customer.Age < 18)
                return false;

            return true;
        }
    }
}