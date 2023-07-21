using FluentValidation;

namespace Packages.Commands
{
    public class SettingsValidator : AbstractValidator<Settings>
    {
        public SettingsValidator()
        {
            RuleFor(options => options.Name)
                   .NotEmpty();

            RuleFor(options => options.Description)
                   .NotEmpty();

            RuleFor(options => options.VaultAddress)
                   .NotEmpty();

            RuleFor(options => options.VaultToken)
                   .NotEmpty();

            RuleFor(options => options.MaxMessagesProcessingInstance)
                   .GreaterThan((ushort)0);
        }
    }
}