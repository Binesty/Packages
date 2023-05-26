using Packages.Commands;

namespace Microservices
{
    public class Sale : Context
    {
        public override string Description => "Represents the sale of cars made to a customer";

        public Vendor Vendor { get; set; } = null!;

        public IEnumerable<Car> Cars { get; set; } = null!;

        public Customer Customer { get; set; } = null!;

        public DateTime? Date { get; set; } = null!;

        public decimal Price { get; set; }
    }

    public record Car(string Model, string Name, int Year, decimal Price);
    public record Vendor(string Code, string FirstName, string Lastname);
    public record Customer(string FirstName, string Lastname, short Age);
}