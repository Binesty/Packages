using FluentValidation;

namespace Packages.Microservices.Validators
{
    public class SettingsValidator : AbstractValidator<Settings>
    {
        public SettingsValidator()
        {
            RuleFor(options => options.Name)
                   .NotEmpty();

            RuleFor(options => options.Description)
                   .NotEmpty();

            RuleFor(options => options.MaxMessagesProcessingInstance)
                   .GreaterThan((ushort)0);
        }
    }
}