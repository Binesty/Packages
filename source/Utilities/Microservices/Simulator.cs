using Microservices.Commands;
using Packages.Commands;
using RabbitMQ.Client;
using System.Security.Cryptography;
using System.Text.Json;

namespace Microservices
{
    internal class Simulator
    {
        private static IBasicProperties _basicProperties = null!;
        private static IModel _channel = null!;
        private static ISettings _settings = null!;
        private static ConnectionFactory _connectionFactory = null!;

        public static async Task Start(ISettings settings)
        {
            _settings = settings;

            _connectionFactory = new()
            {
                HostName = _settings.BrokerSettings.Host,
                UserName = _settings.BrokerSettings.User,
                Password = _settings.BrokerSettings.Password,
                Port = _settings.BrokerSettings.Port
            };

            Console.WriteLine($"Simulator to send messages to: {_settings.Name}");

            var periodicTime = new PeriodicTimer(TimeSpan.FromMilliseconds(5000));

            _channel = _connectionFactory.CreateConnection()
                                         .CreateModel();

            _basicProperties = _channel.CreateBasicProperties();
            _basicProperties.Persistent = true;

            while (await periodicTime.WaitForNextTickAsync())
            {
                SendCommand();
            }
        }

        private static void SendCommand()
        {
            var sale = RetrieveRadomSale();

            Message message = new()
            {
                Id = Guid.NewGuid().ToString(),
                Owner = "simulator",
                Type = MessageType.Command,
                Destination = _settings.Name,
                Operation = nameof(Sell),
                Date = DateTime.Now,
                Content = JsonSerializer.Serialize(sale)
            };

            string exchange = $"entry-{_settings.Name}";
            var headers = new Dictionary<string, string>
            {
                { "microservice", _settings.Name }
            };

            _channel.BasicPublish(exchange, _settings.Name, _basicProperties, JsonSerializer.SerializeToUtf8Bytes(message));

            Console.WriteLine($"Message Sale: {message.Id}");
        }

        private static object RetrieveRadomSale()
        {
            return
            new Sale()
            {
                Cars = GetRandomCars(),
                Vendor = GetRandomVendor(),
                Customer = GetRandomCustomer()
            };
        }

        private static Customer GetRandomCustomer() =>
            Customers.ElementAt(RandomNumberGenerator.GetInt32(0, Customers.Count - 1));

        private static Vendor GetRandomVendor() =>
            Vendors.ElementAt(RandomNumberGenerator.GetInt32(0, Vendors.Count - 1));

        private static IEnumerable<Car> GetRandomCars()
        {
            var cars = new List<Car>();
            int count = RandomNumberGenerator.GetInt32(1, Cars.Count - 1);

            while (cars.Count < count)
            {
                var car = Cars.ElementAt(RandomNumberGenerator.GetInt32(0, Cars.Count - 1));
                if (cars.Contains(car))
                    continue;

                cars.Add(car);
            }

            return cars;
        }

        #region Data

        public static List<Car> Cars => new()
        {
            new Car("Ford", "Fusion", 2019, 110_000),
            new Car("Volkswagen", "Golf", 2016, 70_000),
            new Car("Fiat", "Palio", 2000, 14_000),
            new Car("Chevrolet", "Onix", 2019, 65_000),
            new Car("Jeep", "Compass", 2017, 120_000),
            new Car("Mercedes", "GLA", 2020, 260_000),
            new Car("Hyndai", "HB20", 2017, 89_000),
            new Car("Toyota", "Corolla", 2022, 105_000),
            new Car("BMW", "X6", 2023, 320_000)
        };

        public static List<Vendor> Vendors => new()
        {
            new Vendor("00021", "Peter", "Orchired"),
            new Vendor("00024", "Anna", "Yuuah"),
            new Vendor("00032", "John", "Nikkerr"),
            new Vendor("00012", "Marcos", "Oliveira")
        };

        public static List<Customer> Customers => new()
        {
            new Customer("Steve", "Golth", 19),
            new Customer("Clauber", "Soul", 17),
            new Customer("Riber", "Cliff", 22),
            new Customer("Jonn", "Crow", 32),
            new Customer("Bianca", "Sibre", 40),
            new Customer("Julia", "Cloudde", 68),
            new Customer("Bernard", "Stall", 41)
        };

        #endregion Data
    }
}