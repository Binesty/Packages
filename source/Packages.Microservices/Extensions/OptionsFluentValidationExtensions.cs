using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Packages.Microservices.Extensions
{
    public static class OptionsFluentValidationExtensions
    {
        public static OptionsBuilder<TOptions> ValidateFluently<TOptions>(this OptionsBuilder<TOptions> optionsBuilder)
            where TOptions : class
        {
            optionsBuilder.Services.AddValidatorsFromAssemblyContaining<Settings>(ServiceLifetime.Singleton);
            optionsBuilder.Services.AddSingleton<IValidateOptions<TOptions>>(provider =>
                       {
                           var validator = provider.GetRequiredService<IValidator<TOptions>>();
                           return new FluentValidationOptions<TOptions>(optionsBuilder.Name, validator);
                       });

            return optionsBuilder;
        }
    }

    public class FluentValidationOptions<TOptions> : IValidateOptions<TOptions> where TOptions : class
    {
        private readonly IValidator<TOptions> _validator;
        public string? Name { get; }

        public FluentValidationOptions(string? name, IValidator<TOptions> validator)
        {
            Name = name;
            _validator = validator;
        }

        ValidateOptionsResult IValidateOptions<TOptions>.Validate(string? name, TOptions options)
        {
            if (Name != null && Name != name)
            {
                return ValidateOptionsResult.Skip;
            }

            ArgumentNullException.ThrowIfNull(options);

            var validateResult = _validator.Validate(options);

            if (validateResult.IsValid)
            {
                return ValidateOptionsResult.Success;
            }

            var errors = validateResult.Errors.Select(error =>
                $"Configuration error on {Settings.SectionName} validation failed :'{error.PropertyName}' with error: '{error.ErrorMessage}'");

            return ValidateOptionsResult.Fail(errors);
        }
    }
}