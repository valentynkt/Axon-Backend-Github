using System.Linq.Expressions;
using System.Reflection;
using BuildingBlocks.Core.Domain.Primitives;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace BuildingBlocks.Infrastructure.Persistence.StrongIds;

/// <summary>
/// Generic value comparer for StrongId types.
/// Compares by underlying primitive value and creates snapshots via constructor.
/// </summary>
public sealed class StrongIdValueComparer<TStrongId, TPrimitive> : ValueComparer<TStrongId>
    where TStrongId : StrongId<TPrimitive>
    where TPrimitive : struct, IComparable<TPrimitive>, IEquatable<TPrimitive>
{
    private static readonly Func<TPrimitive, TStrongId> _factory = CompileFactory();

    public StrongIdValueComparer() : base(
        equalsExpression: (left, right) => left!.Value.Equals(right!.Value),
        hashCodeExpression: strongId => strongId!.Value.GetHashCode(),
        snapshotExpression: strongId => _factory(strongId.Value))
    {
    }

    private static Func<TPrimitive, TStrongId> CompileFactory()
    {
        // Find constructor that takes TPrimitive parameter
        var ctor = typeof(TStrongId)
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(c =>
            {
                var parameters = c.GetParameters();
                return parameters.Length == 1 && parameters[0].ParameterType == typeof(TPrimitive);
            });

        if (ctor is null)
            throw new InvalidOperationException(
                $"{typeof(TStrongId).Name} must have a constructor that accepts {typeof(TPrimitive).Name}.");

        // Compile expression: (TPrimitive value) => new TStrongId(value)
        var param = Expression.Parameter(typeof(TPrimitive), "value");
        var newExpression = Expression.New(ctor, param);
        return Expression.Lambda<Func<TPrimitive, TStrongId>>(newExpression, param).Compile();
    }
}