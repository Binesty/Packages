using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;
using System.Linq.Expressions;

namespace Packages.Commands
{
    internal sealed class Cosmos<TContext> : IRepository where TContext : Context
    {
        private readonly string Name;
        string IRepository.Name => Name;

        private const short MaxDegreeOfParallelism = 3;

        private readonly IOptions<Settings> _settings;
        private readonly Broker<TContext> _broker;
        private readonly Secrets _secrets;
        private readonly CosmosClient? CosmosClient;
        private readonly Database? Database;
        private readonly Container? Contexts;
        private readonly Container? Subscriptions;

        internal Cosmos(IOptions<Settings> settings, Secrets secrets, Broker<TContext> broker)
        {
            _settings = settings;
            _secrets = secrets;
            _broker = broker;

            Name = _settings.Value.Name;

            CosmosClient = new CosmosClient(_secrets.CosmosEndPoint, _secrets.CosmosPrimaryKey, GetOptions());

            CosmosClient.CreateDatabaseIfNotExistsAsync(Name).GetAwaiter()
                                                             .GetResult();

            Database = CosmosClient?.GetDatabase(Name);
            Contexts = Database?.CreateContainerIfNotExistsAsync(nameof(Contexts), "/name")
                                .GetAwaiter()
                                .GetResult();

            Subscriptions = Database?.CreateContainerIfNotExistsAsync(nameof(Subscriptions), "/subscriber")
                                     .GetAwaiter()
                                     .GetResult();

            Contexts = CosmosClient?.GetContainer(Name, nameof(Contexts));
            Subscriptions = CosmosClient?.GetContainer(Name, nameof(Subscriptions));
        }

        private static CosmosClientOptions GetOptions()
        {
            return
            new CosmosClientOptions()
            {
                SerializerOptions = new CosmosSerializationOptions()
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                },
                AllowBulkExecution = true,
                ConnectionMode = ConnectionMode.Gateway,
                RequestTimeout = TimeSpan.FromSeconds(30)
            };
        }

        private Container? GetContainer(StorableType storableType)
        {
            return
            storableType switch
            {
                StorableType.Contexts => Contexts,
                StorableType.Subscriptions => Subscriptions,

                _ => default,
            };
        }

        private static async ValueTask<ItemResponse<TStorable>> Create<TStorable>(Container container, IStorable storable) where TStorable : IStorable
        {
            return await container.UpsertItemAsync<TStorable>(storable as dynamic, new PartitionKey(storable.Partition));
        }

        private static async ValueTask<ItemResponse<TStorable>> Replace<TStorable>(Container container, IStorable storable) where TStorable : IStorable
        {
            return await container.UpsertItemAsync<TStorable>(storable as dynamic, new PartitionKey(storable.Partition));
        }

        private static async ValueTask<ItemResponse<TStorable>> Delete<TStorable>(Container container, IStorable storable) where TStorable : IStorable
        {
            return await container.DeleteItemAsync<TStorable>(storable.Id, new PartitionKey(storable.Partition));
        }

        private static Task<ItemResponse<TStorable>> BulkCreate<TStorable>(Container container, IStorable storable) where TStorable : IStorable
        {
            return container.UpsertItemAsync<TStorable>(storable as dynamic, new PartitionKey(storable.Partition));
        }

        private static Task<ItemResponse<TStorable>> BulkReplace<TStorable>(Container container, IStorable storable) where TStorable : IStorable
        {
            return container.UpsertItemAsync<TStorable>(storable as dynamic, new PartitionKey(storable.Partition));
        }

        private static Task<ItemResponse<TStorable>> BulkDelete<TStorable>(Container container, IStorable storable) where TStorable : IStorable
        {
            return container.DeleteItemAsync<TStorable>(storable.Id, new PartitionKey(storable.Partition));
        }

        async Task<IStorable?> IRepository.Save<TStorable>(IStorable storable)
        {
            var container = GetContainer(storable.StorableType);
            if (container is null)
                return default;

            var document = storable.StorableStatus switch
            {
                StorableStatus.New => await Create<TStorable>(container, storable),
                StorableStatus.Changed => await Replace<TStorable>(container, storable),
                StorableStatus.Deleted => await Delete<TStorable>(container, storable),

                _ => default,
            };

            if (document is not null && document.Resource is not null)
            {
                document.Resource.StorableStatus = StorableStatus.NotChanged;
                return document.Resource;
            }

            return default;
        }

        async Task<IEnumerable<TStorable>> IRepository.BulkSave<TStorable>(IEnumerable<IStorable> storables)
        {
            var container = GetContainer(storables.FirstOrDefault()?.StorableType ?? StorableType.Contexts);
            IList<TStorable> saved = new List<TStorable>();

            if (container is not null)
            {
                var tasks = new List<Task<(ItemResponse<TStorable>?, Exception?)>>();

                foreach (var storable in storables)
                {
                    var task = storable.StorableStatus switch
                    {
                        StorableStatus.New => BulkCreate<TStorable>(container, storable),
                        StorableStatus.Changed => BulkReplace<TStorable>(container, storable),
                        StorableStatus.Deleted => BulkDelete<TStorable>(container, storable),

                        _ => default,
                    };

                    if (task is not null)
                        tasks.Add(ExecuteAndCaptureErrors(task));
                }

                await Task.WhenAll(tasks);
            }

            return saved;
        }

        private async Task<(ItemResponse<TStorable>?, Exception?)> ExecuteAndCaptureErrors<TStorable>(Task<ItemResponse<TStorable>> operation) where TStorable : IStorable
        {
            ItemResponse<TStorable>? result = null; 

            try
            {
                result = await operation;

                _broker.ConfirmDelivery(result.Resource.DeliveryTag);
                
                if (result.Resource is TContext context)
                    _broker.SendReplications(context);

                return (result, default);
            }
            catch (Exception exception)
            {             
                _broker?.PublishError(new Message() { Content = exception.Message });

                if (result is not null)
                    _broker?.RejectDelivery(result.Resource.DeliveryTag);

                return (default, exception);
            }
        }

        async Task<IEnumerable<TStorable>> IRepository.Fetch<TStorable>(Expression<Func<TStorable, bool>> expression, StorableType storableType, Expression<Func<TStorable, bool>>? optionalExpression, int units)
        {
            var container = GetContainer(storableType);
            if (container is null)
                return Enumerable.Empty<TStorable>();

            var items = new List<TStorable>();

            var queryable = container.GetItemLinqQueryable<TStorable>(true).Where(expression);

            queryable = optionalExpression is not null ? queryable.Where(optionalExpression) : queryable;
            queryable = (units > 0) ? queryable.Take(units) : queryable;

            using FeedIterator<TStorable> feedIterator = queryable.ToFeedIterator();
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<TStorable> currentResultSet = await feedIterator.ReadNextAsync();
                Parallel.ForEach(currentResultSet, item =>
                {
                    item.StorableStatus = StorableStatus.NotChanged;
                    items.Add(item);
                });
            }

            return items;
        }
    }
}