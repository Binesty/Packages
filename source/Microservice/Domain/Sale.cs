﻿using Packages.Microservices.Domain;

namespace Microservice.Domain
{
    public class Sale : Context
    {
        public Vendor Vendor { get; set; } = null!;

        public Store Store { get; set; } = null!;

        public IEnumerable<Car> Cars { get; set; } = null!;

        public Customer Customer { get; set; } = null!;

        public DateTime? Date { get; set; } = null!;

        public decimal Price { get; set; } = decimal.Zero;

        public bool Active { get; set; } = true;
    }

    public record Car(string Model, string Name, int Year, decimal Price);
    public record Vendor(string Code, string FirstName, string Lastname);
    public record Customer(string FirstName, string Lastname, short Age);
    public record Store(string Code, string Name, string Location);
}