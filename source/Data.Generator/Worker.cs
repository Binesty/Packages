using Microsoft.Extensions.Options;
using Packages.Microservices;
using Packages.Microservices.Domain;
using Packages.Microservices.Messages;
using RabbitMQ.Client;
using Sample.Commands;
using Sample.Commands.Commands;
using Sample.Commands.Replications;
using System.Security.Cryptography;
using System.Text.Json;

namespace Data.Generator
{
    public class Worker : BackgroundService
    {
        private const short requestedHeartbeatSeconds = 10;

        private BrokerCommunication.Configuration _brokerCommunicationConfigurationMicroservice = BrokerCommunication.GetConfiguration("car-sale");
        private BrokerCommunication.Configuration _brokerCommunicationConfigurationManufacturing = BrokerCommunication.GetConfiguration("manufacturing");
        private BrokerCommunication.Configuration _brokerCommunicationConfigurationCommunication = BrokerCommunication.GetConfiguration("communication");

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
                    _logger.LogInformation("Data Generator start");

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
                        SendSubscription(_brokerCommunicationConfigurationCommunication.QueueName, nameof(CarEndManufacturing), new List<string>() { "Store" });
                        SendSubscription(_brokerCommunicationConfigurationManufacturing.QueueName, nameof(Sell), new List<string>() { "Cars", "Store", "Date" });
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

            _logger.LogInformation("Data Generator to send messages to {microservice}", _brokerCommunicationConfigurationMicroservice.QueueName);

            _connectionFactory = new()
            {
                HostName = secrets.RabbitHost,
                UserName = secrets.RabbitUser,
                Password = secrets.RabbitPassword,
                Port = secrets.RabbitPort,
                ClientProvidedName = "data-generator",
                RequestedHeartbeat = TimeSpan.FromMicroseconds(requestedHeartbeatSeconds)
            };

            _channel = _connectionFactory.CreateConnection()
                                         .CreateModel();

            _logger.LogInformation("Clean all queues");
            CreateQueuesReplications();
        }

        private void DeleteQueues()
        {
            _channel.QueuePurge(_brokerCommunicationConfigurationMicroservice.QueueName);
            _channel.QueuePurge(_brokerCommunicationConfigurationCommunication.QueueName);
            _channel.QueuePurge(_brokerCommunicationConfigurationManufacturing.QueueName);
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
                Owner = _brokerCommunicationConfigurationManufacturing.QueueName,
                Type = MessageType.Replication,
                Destination = _brokerCommunicationConfigurationMicroservice.QueueName,
                Operation = nameof(CarEndManufacturing),
                Date = DateTime.UtcNow,
                Content = JsonSerializer.Serialize(replication)
            };

            var headers = new Dictionary<string, object>
            {
                {
                  _brokerCommunicationConfigurationMicroservice.Header,
                  _brokerCommunicationConfigurationMicroservice.HeaderValue
                }
            };
            IBasicProperties _basicProperties = _channel.CreateBasicProperties();
            _basicProperties.Persistent = true;
            _basicProperties.Headers = headers;

            _channel.BasicPublish(_brokerCommunicationConfigurationMicroservice.ExchangeEntry,
                                  _brokerCommunicationConfigurationMicroservice.QueueName,
                                  _basicProperties, JsonSerializer.SerializeToUtf8Bytes(message));

            Console.WriteLine($"Replication from {_brokerCommunicationConfigurationManufacturing.QueueName}: {replication.Id}");
        }

        private void CreateQueuesReplications()
        {
            _channel.QueueDeclareNoWait(_brokerCommunicationConfigurationCommunication.QueueName,
            durable: true, exclusive: false,
                                        autoDelete: false, arguments:
                                        new Dictionary<string, object> { { _brokerCommunicationConfigurationCommunication.Header,
                                                                           _brokerCommunicationConfigurationCommunication.HeaderValue } });

            _channel.QueueDeclareNoWait(_brokerCommunicationConfigurationManufacturing.QueueName,
                                        durable: true, exclusive: false,
                                        autoDelete: false, arguments:
                                        new Dictionary<string, object> { { _brokerCommunicationConfigurationManufacturing.Header,
                                                                           _brokerCommunicationConfigurationManufacturing.HeaderValue } });
        }

        private void SendCommand()
        {
            var sale = CreateRadomSale();

            Message message = new()
            {
                Id = Guid.NewGuid().ToString(),
                Owner = "data-generator",
                Type = MessageType.Command,
                Destination = _brokerCommunicationConfigurationMicroservice.QueueName,
                Operation = nameof(Sell),
                Date = DateTime.UtcNow,
                Content = JsonSerializer.Serialize(sale)
            };

            var headers = new Dictionary<string, object>
            {
                { _brokerCommunicationConfigurationMicroservice.Header,
                  _brokerCommunicationConfigurationMicroservice.HeaderValue
                }
            };
            IBasicProperties _basicProperties = _channel.CreateBasicProperties();
            _basicProperties.Persistent = true;
            _basicProperties.Headers = headers;

            _channel.BasicPublish(_brokerCommunicationConfigurationMicroservice.ExchangeEntry,
                                  _brokerCommunicationConfigurationMicroservice.QueueName,
                                  _basicProperties, JsonSerializer.SerializeToUtf8Bytes(message));

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
                Destination = _brokerCommunicationConfigurationMicroservice.QueueName,
                Operation = nameof(Sell),
                Date = DateTime.UtcNow,
                Content = JsonSerializer.Serialize(subscription)
            };

            var headers = new Dictionary<string, object>
            {
                {
                  _brokerCommunicationConfigurationMicroservice.Header,
                  _brokerCommunicationConfigurationMicroservice.HeaderValue
                }
            };
            IBasicProperties _basicProperties = _channel.CreateBasicProperties();
            _basicProperties.Persistent = true;
            _basicProperties.Headers = headers;

            _channel.BasicPublish(_brokerCommunicationConfigurationMicroservice.ExchangeEntry,
                                  _brokerCommunicationConfigurationMicroservice.QueueName,
                                  _basicProperties, JsonSerializer.SerializeToUtf8Bytes(message));

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