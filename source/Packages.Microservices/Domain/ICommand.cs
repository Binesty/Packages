namespace Packages.Microservices.Domain
{
    public interface ICommand<TContext> where TContext : Context
    {
        string Description { get; }

        TContext? Execute(TContext context);

        bool CanExecute(TContext context);
    }
}