using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using System.Linq.Expressions;

namespace Packages.Commands.Data
{
    internal sealed class Cosmos<TContext> : IRepository where TContext : Context
    {
        private readonly string Name;
        string IRepository.Name => Name;

        private readonly string PrimaryKey;
        private readonly string EndPoint;
        private readonly CosmosClient? CosmosClient;
        private readonly Database? Database;
        private readonly Container? Contexts;

        internal Cosmos(ISettings settings)
        {
            Name = settings.CosmosSettings.Database;
            PrimaryKey = settings.CosmosSettings.PrimaryKey;
            EndPoint = settings.CosmosSettings.EndPoint;

            string contexts = "Contexts";
            string partition = "name";

            CosmosClient = new CosmosClient(EndPoint, PrimaryKey, GetOptions());
            Database = CosmosClient?.GetDatabase(Name);

            Contexts = Database?.CreateContainerIfNotExistsAsync(contexts, $"/{partition}")
                                .GetAwaiter()
                                .GetResult();

            Contexts = CosmosClient?.GetContainer(Name, contexts);
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

                ConnectionMode = ConnectionMode.Gateway,
                RequestTimeout = TimeSpan.FromMilliseconds(60)
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

        async Task<IStorable?> IRepository.Save<TStorable>(IStorable storable)
        {
            if (Contexts is null)
                return default;

            var document = storable.StorableStatus switch
            {
                StorableStatus.New => await Create<TStorable>(Contexts, storable),
                StorableStatus.Changed => await Replace<TStorable>(Contexts, storable),
                StorableStatus.Deleted => await Delete<TStorable>(Contexts, storable),

                _ => default,
            };

            if (document is not null && document.Resource is not null)
            {
                document.Resource.StorableStatus = StorableStatus.NotChanged;
                return document.Resource;
            }

            return default;
        }

        async Task<IEnumerable<TStorable>> IRepository.Fetch<TStorable>(Expression<Func<TStorable, bool>> expression, int units)
        {
            if (Contexts is null)
                return Enumerable.Empty<TStorable>();

            var items = new List<TStorable>();

            var queryable = Contexts.GetItemLinqQueryable<TStorable>(true).Where(expression);
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