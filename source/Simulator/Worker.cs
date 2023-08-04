using Microservice.Domain;
using Microservice.Domain.Commands;
using Microservice.Domain.Replications;
using Microsoft.Extensions.Options;
using Packages.Commands;
using RabbitMQ.Client;
using System.Security.Cryptography;
using System.Text.Json;

namespace Simulator
{
    public class Worker : BackgroundService
    {
        private const short requestedHeartbeatSeconds = 10;
        private const string microservice = "car-sale";
        private const string header = "microservice";
        private const string exchangeEntryPrefix = "entry";
        private const string microserviceManufacturing = "manufacturing";
        private const string microserviceCommunication = "communication";

        private ConnectionFactory _connectionFactory = null!;
        private IModel _channel = null!;

        private readonly IOptions<Settings> _settings;
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger, IOptions<Settings> settings)
        {
            _logger = logger;
            _settings = settings;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Configure();

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Run(() =>
                {
                    Console.Clear();
                    _logger.LogInformation("Simulator start");

                    DeleteQueues();

                    _logger.LogInformation("Input total messages");
                    var dataRead = Console.ReadLine();

                    if (dataRead?.ToLower() == "r")
                    {
                        SendReplication();
                        return;
                    }

                    if (dataRead?.ToLower() == "s")
                    {
                        SendSubscription(microserviceCommunication, nameof(CarEndManufacturing), new List<string>() { "Store" });
                        SendSubscription(microserviceManufacturing, nameof(Sell), new List<string>() { "Cars", "Store", "Date" });
                        return;
                    }

                    int total = int.TryParse(dataRead, out total) ? total : 0;
                    Parallel.For(0, total, count =>
                    {
                        SendCommand();
                    });

                    _logger.LogInformation("key to continue...");
                    Console.ReadLine();
                }, stoppingToken);
            }
        }

        public void Configure()
        {
            _logger.LogInformation("Get Secrets...");
            var secrets = Secret.Loaded;

            _logger.LogInformation("Simulator to send messages to {microservice}", microservice);

            _connectionFactory = new()
            {
                HostName = secrets.RabbitHost,
                UserName = secrets.RabbitUser,
                Password = secrets.RabbitPassword,
                Port = secrets.RabbitPort,
                ClientProvidedName = "simulator",
                RequestedHeartbeat = TimeSpan.FromMicroseconds(requestedHeartbeatSeconds)
            };

            _channel = _connectionFactory.CreateConnection()
                                         .CreateModel();

            _logger.LogInformation("Clean all queues");
            CreateQueuesReplications();
        }

        private void DeleteQueues()
        {
            _channel.QueuePurge(microserviceCommunication);
            _channel.QueuePurge(microserviceManufacturing);
            _channel.QueuePurge(microservice);
        }

        private void SendReplication()
        {
            Replication replication = new()
            {
                Id = Guid.NewGuid().ToString(),
                Content = new
                {
                    Model = "Mercedes",
                    Name = "GLA"
                }
            };

            Message message = new()
            {
                Id = Guid.NewGuid().ToString(),
                Owner = microserviceManufacturing,
                Type = MessageType.Replication,
                Destination = microservice,
                Operation = nameof(CarEndManufacturing),
                Date = DateTime.UtcNow,
                Content = JsonSerializer.Serialize(replication)
            };

            string exchange = $"{microservice}-{exchangeEntryPrefix}";
            var headers = new Dictionary<string, object>
            {
                { header, microservice }
            };
            IBasicProperties _basicProperties = _channel.CreateBasicProperties();
            _basicProperties.Persistent = true;
            _basicProperties.Headers = headers;

            _channel.BasicPublish(exchange, microservice, _basicProperties, JsonSerializer.SerializeToUtf8Bytes(message));

            Console.WriteLine($"Replication from {microserviceManufacturing}: {replication.Id}");
        }

        private void CreateQueuesReplications()
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

        private void SendCommand()
        {
            var sale = CreateRadomSale();

            Message message = new()
            {
                Id = Guid.NewGuid().ToString(),
                Owner = "simulator",
                Type = MessageType.Command,
                Destination = microservice,
                Operation = nameof(Sell),
                Date = DateTime.UtcNow,
                Content = JsonSerializer.Serialize(sale)
            };

            string exchange = $"{microservice}-{exchangeEntryPrefix}";
            var headers = new Dictionary<string, object>
            {
                { header, microservice }
            };
            IBasicProperties _basicProperties = _channel.CreateBasicProperties();
            _basicProperties.Persistent = true;
            _basicProperties.Headers = headers;

            _channel.BasicPublish(exchange, microservice, _basicProperties, JsonSerializer.SerializeToUtf8Bytes(message));

            Console.WriteLine($"Message Sale: {message.Id}");
        }

        private void SendSubscription(string name, string operation, List<string> fields)
        {
            Subscription subscription = new()
            {
                Id = Guid.NewGuid().ToString(),
                Subscriber = name,
                Date = DateTime.UtcNow,
                Operataion = operation,
                Active = true,
                Fields = fields
            };

            Message message = new()
            {
                Id = Guid.NewGuid().ToString(),
                Owner = name,
                Type = MessageType.Subscription,
                Destination = microservice,
                Operation = nameof(Sell),
                Date = DateTime.UtcNow,
                Content = JsonSerializer.Serialize(subscription)
            };

            string exchange = $"{microservice}-{exchangeEntryPrefix}";
            var headers = new Dictionary<string, object>
            {
                { header, microservice }
            };
            IBasicProperties _basicProperties = _channel.CreateBasicProperties();
            _basicProperties.Persistent = true;
            _basicProperties.Headers = headers;

            _channel.BasicPublish(exchange, microservice, _basicProperties, JsonSerializer.SerializeToUtf8Bytes(message));

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
            new Customer("Riber", "Cliff", 22),
            new Customer("Jonn", "Crow", 32),
            new Customer("Bianca", "Sibre", 40),
            new Customer("Julia", "Cloudde", 68),
            new Customer("Bernard", "Stall", 41)
        };

        #endregion Data
    }
}