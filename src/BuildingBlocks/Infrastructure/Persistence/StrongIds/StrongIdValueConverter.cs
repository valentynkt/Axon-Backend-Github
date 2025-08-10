using System.Linq.Expressions;
using System.Reflection;
using BuildingBlocks.Core.Domain.Primitives;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BuildingBlocks.Infrastructure.Persistence.StrongIds;

/// <summary>
/// Generic value converter for StrongId types to their primitive values.
/// Uses compiled expression delegates for fast construction.
/// </summary>
public sealed class StrongIdValueConverter<TStrongId, TPrimitive> : ValueConverter<TStrongId, TPrimitive>
    where TStrongId : StrongId<TPrimitive>
    where TPrimitive : struct, IComparable<TPrimitive>, IEquatable<TPrimitive>
{
    private static readonly Func<TPrimitive, TStrongId> _factory = CompileFactory();

    public StrongIdValueConverter() : base(
        strongId => strongId.Value,
        primitiveValue => _factory(primitiveValue))
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