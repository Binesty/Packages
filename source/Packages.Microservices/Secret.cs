namespace Packages.Microservices
{
    public class Secret
    {
        public static Content Loaded { get; internal set; } = null!;

        public Content? Data { get; set; }

        public record Content(string CosmosPrimaryKey,
                              string CosmosEndPoint,
                              string RabbitHost,
                              string RabbitUser,
                              string RabbitPassword,
                              int RabbitPort);
    }
}