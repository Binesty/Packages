using System.Linq.Expressions;

namespace Packages.Commands
{
    internal interface IRepository
    {
        internal string Name { get; }

        internal Task<IStorable?> Save<TStorable>(IStorable storable) where TStorable : IStorable;

        internal Task<IEnumerable<TStorable>> Fetch<TStorable>(Expression<Func<TStorable, bool>> expression, StorableType storableType, int units = 0) where TStorable : IStorable;
    }
}