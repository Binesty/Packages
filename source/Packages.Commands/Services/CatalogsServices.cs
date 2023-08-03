using System.Net.Http.Json;

namespace Packages.Commands.Services
{
    public class CatalogsServices
    {
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
