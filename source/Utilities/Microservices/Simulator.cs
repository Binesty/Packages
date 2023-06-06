using Microservices.Commands;
using Microservices.Replications;
using Packages.Commands;
using RabbitMQ.Client;
using System.Security.Cryptography;
using System.Text.Json;

namespace Microservices
{
    internal class Simulator
    {
        private const string header = "microservice";
        private const string exchangeEntryPrefix = "entry";
        private const string microserviceManufacturing = "manufacturing";
        private const string microserviceCommunication = "communication";

        private static ISettings _settings = null!;

        private static ConnectionFactory _connectionFactory = null!;
        private static IModel _channel = null!;

        public static async Task Start(ISettings settings)
        {
            _settings = settings;

            Console.WriteLine($"Simulator to send messages to: {_settings.Name}");

            _connectionFactory = new()
            {
                HostName = _settings.BrokerSettings.Host,
                UserName = _settings.BrokerSettings.User,
                Password = _settings.BrokerSettings.Password,
                Port = _settings.BrokerSettings.Port
            };

            _channel = _connectionFactory.CreateConnection()
                                         .CreateModel();

            CreateQueuesReplications();
            Task.Delay(TimeSpan.FromSeconds(1)).Wait();

            var periodicTime = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));

            int count = 0;
            while (await periodicTime.WaitForNextTickAsync())
            {
                if (count == 2)
                    break;

                if (count == 0)
                    SendSubscription(microserviceCommunication);

                if (count == 0)
                    SendSubscription(microserviceManufacturing);

                SendCommand();
                SendReplication();

                count++;
            }
        }

        private static void SendReplication()
        {
            Replication replication = new()
            {
                Id = Guid.NewGuid().ToString(),
                Content = new
                {
                    Id = Guid.NewGuid().ToString(),
                    Model = "Mercedes",
                    Name = "GLA"
                }
            };

            Message message = new()
            {
                Id = Guid.NewGuid().ToString(),
                Owner = microserviceManufacturing,
                Type = MessageType.Replication,
                Destination = _settings.Name,
                Operation = nameof(CarEndManufacturing),
                Date = DateTime.UtcNow,
                Content = JsonSerializer.Serialize(replication)
            };

            string exchange = $"{_settings.Name}-{exchangeEntryPrefix}";
            var headers = new Dictionary<string, object>
            {
                { header, _settings.Name }
            };

            IBasicProperties _basicProperties = _channel.CreateBasicProperties();
            _basicProperties.Persistent = true;
            _basicProperties.Headers = headers;

            _channel.BasicPublish(exchange, _settings.Name, _basicProperties, JsonSerializer.SerializeToUtf8Bytes(message));

            Console.WriteLine($"Replication from {microserviceManufacturing}: {replication.Content.Id}");
        }

        private static void CreateQueuesReplications()
        {
            _channel.QueueDeclareNoWait(microserviceCommunication,
                                        durable: true, exclusive: false,
                                        autoDelete: false, arguments:
                                        new Dictionary<string, object> { { header, microserviceCommunication } });

            _channel.QueueDeclareNoWait(microserviceManufacturing,
                                        durable: true, exclusive: false,
                                        autoDelete: false, arguments:
                                        new Dictionary<string, object> { { header, microserviceCommunication } });
        }

        private static void SendCommand()
        {
            var sale = CreateRadomSale();

            Message message = new()
            {
                Id = Guid.NewGuid().ToString(),
                Owner = "simulator",
                Type = MessageType.Command,
                Destination = _settings.Name,
                Operation = nameof(Sell),
                Date = DateTime.UtcNow,
                Content = JsonSerializer.Serialize(sale)
            };

            string exchange = $"{_settings.Name}-{exchangeEntryPrefix}";
            var headers = new Dictionary<string, object>
            {
                { header, _settings.Name }
            };

            IBasicProperties _basicProperties = _channel.CreateBasicProperties();
            _basicProperties.Persistent = true;
            _basicProperties.Headers = headers;

            _channel.BasicPublish(exchange, _settings.Name, _basicProperties, JsonSerializer.SerializeToUtf8Bytes(message));

            Console.WriteLine($"Message Sale: {message.Id}");
        }

        private static void SendSubscription(string name)
        {
            Subscription subscription = new()
            {
                Id = Guid.NewGuid().ToString(),
                Subscriber = name,
                Date = DateTime.UtcNow,
                Command = nameof(Sell),
                Active = true,
                Fields = new List<string>() { "Cars", "Store", "Date" }
            };

            Message message = new()
            {
                Id = Guid.NewGuid().ToString(),
                Owner = name,
                Type = MessageType.Subscription,
                Destination = _settings.Name,
                Operation = nameof(Sell),
                Date = DateTime.UtcNow,
                Content = JsonSerializer.Serialize(subscription)
            };

            string exchange = $"{_settings.Name}-{exchangeEntryPrefix}";
            var headers = new Dictionary<string, object>
            {
                { header, _settings.Name }
            };

            IBasicProperties _basicProperties = _channel.CreateBasicProperties();
            _basicProperties.Persistent = true;
            _basicProperties.Headers = headers;

            _channel.BasicPublish(exchange, _settings.Name, _basicProperties, JsonSerializer.SerializeToUtf8Bytes(message));

            Console.WriteLine($"Subscription from {name}: {subscription.Id}");
        }

        private static Sale CreateRadomSale()
        {
            return
            new Sale()
            {
                Cars = GetRandomCars(),
                Vendor = GetRandomVendor(),
                Customer = GetRandomCustomer(),
                Store = GetRandomStore()
            };
        }

        private static Customer GetRandomCustomer() =>
            Customers.ElementAt(RandomNumberGenerator.GetInt32(0, Customers.Count - 1));

        private static Vendor GetRandomVendor() =>
            Vendors.ElementAt(RandomNumberGenerator.GetInt32(0, Vendors.Count - 1));

        private static Store GetRandomStore() =>
            Stores.ElementAt(RandomNumberGenerator.GetInt32(0, Stores.Count - 1));

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

        public static List<Store> Stores => new()
        {
            new Store("001", "CB-VAC", "Brazil"),
            new Store("002", "STORE-SSO", "USA"),
            new Store("003", "RITT03", "Spain"),
            new Store("004", "CARSTORE", "China")
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