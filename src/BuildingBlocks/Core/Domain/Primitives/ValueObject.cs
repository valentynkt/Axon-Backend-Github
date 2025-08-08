using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Validation;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Domain.Primitives;

public abstract record ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();
    public abstract Validation<Unit> Validate();

    public bool IsValid => Validate().IsValid;
    public IReadOnlyList<Error> ValidationErrors => Validate().Errors;

    public override int GetHashCode()
    {
        var hc = new HashCode();
        foreach (var part in GetEqualityComponents())
            hc.Add(part);
        return hc.ToHashCode();
    }

    public virtual bool Equals(ValueObject? other) =>
        other is not null &&
        other.GetType() == GetType() &&
        GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
}

public abstract record SingleValueObject<T> : ValueObject
{
    public T Value { get; }

    protected SingleValueObject(T value) => Value = value;

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }

    public override string ToString() => Value?.ToString() ?? string.Empty;

    public static implicit operator T(SingleValueObject<T> v) => v.Value!;
}