using System.Net.Http.Json;

namespace Packages.Microservices.Services
{
    public class CatalogsServices
    {
        internal static CatalogsServices? Current { get; set; }

        private readonly HttpClient _httpClient;

        public CatalogsServices(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public Task MicroservicePublish(Contract contract)
        {
            return _httpClient.PostAsJsonAsync("/publish", contract);
        }
    }
}