namespace Packages.Commands
{
    public interface ICommand<TContext> where TContext : Context
    {
        string Description { get; }

        TContext? Execute(TContext context);

        bool Validate(TContext context);
    }
}