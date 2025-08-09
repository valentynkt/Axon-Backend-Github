using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Domain.Primitives;

/// <summary>
/// Base type for value objects (DDD).
/// - Immutable semantics
/// - Equality by components
/// - Requires validation via <see cref="Validate"/>
/// </summary>
public abstract record ValueObject
{
    /// <summary>Provide components used for equality and hashing.</summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    /// <summary>Validates the VO invariants. Must be cheap and side-effect free.</summary>
    public abstract Validation<Unit> Validate();

    public bool IsValid => Validate().IsValid;
    public IReadOnlyList<Error> ValidationErrors => Validate().Errors;

    public override int GetHashCode()
        => GetEqualityComponents()
            .Aggregate(0, (acc, obj) => HashCode.Combine(acc, obj?.GetHashCode() ?? 0));

    public virtual bool Equals(ValueObject? other)
        => other is not null &&
           other.GetType() == GetType() &&
           GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
}