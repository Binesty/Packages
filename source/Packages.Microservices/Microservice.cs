using Microsoft.Extensions.Options;
using Packages.Microservices.Domain;

namespace Packages.Microservices
{
    public static class Microservice<TContext> where TContext : Context
    {
        private static Operator<TContext> _operator = null!;

        public static Operator<TContext> Configure(IOptions<Settings> settings)
        {
            _operator ??= new Operator<TContext>(settings);
            return _operator;
        }
    }
}