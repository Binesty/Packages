using Packages.Microservices.Jobs;

namespace Sample.Jobs.Jobs
{
    public class SaleCanceled : IJob<Sale>
    {
        public string Description => "Canceled sales";
    }
}
