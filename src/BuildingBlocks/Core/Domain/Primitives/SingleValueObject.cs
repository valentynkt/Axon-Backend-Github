using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Domain.Primitives;

/// <summary>
/// Convenience base for single-value value objects.
/// Requires validated factory methods in derived types.
/// </summary>
public abstract record SingleValueObject<T> : ValueObject
{
    public T Value { get; }

    /// <summary>Protected ctor; prefer static factory methods on derived types.</summary>
    protected SingleValueObject(T value) => Value = value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>
    /// Base implementation returns Valid; derived types should override if they
    /// want runtime re-validation (most enforce invariants in factories).
    /// </summary>
    public override ValidationResult<Unit> Validate() => ValidationResult<Unit>.CreateValid(Unit.Value);

    public override string ToString() => Value?.ToString() ?? string.Empty;

    public static implicit operator T(SingleValueObject<T> vo) => vo.Value;
}