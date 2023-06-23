using Packages.Commands;

namespace Microservices
{
    public class Contract : IContract
    {
        public string Name => "cars-sale";

        public string Description => "Implementation with Commands of Car Sale";
    };
}