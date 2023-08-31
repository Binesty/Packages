namespace Packages.Microservices.Commands
{
    public interface ICommand<TContext> where TContext : Context
    {
        string Description { get; }

        TContext? Execute(TContext context);

        bool CanExecute(TContext context);
    }
}