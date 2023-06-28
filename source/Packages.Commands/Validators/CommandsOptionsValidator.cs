using FluentValidation;

namespace Packages.Commands
{
    public class CommandsOptionsValidator : AbstractValidator<CommandsOptions>
    {
        public CommandsOptionsValidator()
        {
            RuleFor(options => options.Name)
                   .NotEmpty();

            RuleFor(options => options.Description)
                   .NotEmpty();

            RuleFor(options => options.CosmosPrimaryKey)
                   .NotEmpty();

            RuleFor(options => options.CosmosEndPoint)
                   .NotEmpty();

            RuleFor(options => options.RabbitHost)
                   .NotEmpty();

            RuleFor(options => options.RabbitUser)
                   .NotEmpty();

            RuleFor(options => options.RabbitPassword)
                   .NotEmpty();

            RuleFor(options => options.RabbitPort)
                   .NotEmpty()
                   .GreaterThan(0);
        }
    }
}