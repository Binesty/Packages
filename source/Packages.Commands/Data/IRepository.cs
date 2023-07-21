using System.Linq.Expressions;

namespace Packages.Commands
{
    internal interface IRepository
    {
        internal string Name { get; }

        internal Task<IStorable?> Save<TStorable>(IStorable storable) where TStorable : IStorable;

        internal Task<IEnumerable<TStorable>> BulkSave<TStorable>(IEnumerable<IStorable> storables) where TStorable : IStorable;

        internal Task<IEnumerable<TStorable>> Fetch<TStorable>(Expression<Func<TStorable, bool>> expression, StorableType storableType, Expression<Func<TStorable, bool>>? optionalExpression = null, int units = 0) where TStorable : IStorable;
    }
}