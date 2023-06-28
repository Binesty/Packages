using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Packages.Commands.Extensions
{
    public static class MicroserviceCommandsExtensions
    {
        public static IServiceCollection AddMicroserviceCommands(this IServiceCollection services, HostBuilderContext hostBuilderContext)
        {
            services.AddValidatorsFromAssemblyContaining<Options>(ServiceLifetime.Singleton);

            return services;
        }
    }
}