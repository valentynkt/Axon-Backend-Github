using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Domain.Primitives;

public partial interface IStrongId
{
    object GetValue();
    Type GetValueType();
}

public partial interface IStrongId<TPrimitive> : IStrongId where TPrimitive : struct
{
    TPrimitive Value { get; }
}

/// <summary>
/// Base for strongly-typed IDs. No parameterless constructor magic.
/// </summary>
public abstract record StrongId<TPrimitive> : IStrongId<TPrimitive>, IComparable<StrongId<TPrimitive>>
    where TPrimitive : struct, IComparable<TPrimitive>, IEquatable<TPrimitive>
{
    public TPrimitive Value { get; }

    protected StrongId(TPrimitive value)
    {
        if (EqualityComparer<TPrimitive>.Default.Equals(value, default))
            throw new ArgumentException($"StrongId value cannot be default({typeof(TPrimitive).Name})", nameof(value));

        Value = value;
    }

    public object GetValue() => Value;
    public Type GetValueType() => typeof(TPrimitive);

    public int CompareTo(StrongId<TPrimitive>? other) =>
        other is null ? 1 : Value.CompareTo(other.Value);

    public static implicit operator TPrimitive(StrongId<TPrimitive> id) => id.Value;

    public override string ToString() => Value.ToString() ?? string.Empty;
}

/// <summary>
/// GUID-based strong id.
/// </summary>
public abstract record GuidStrongId : StrongId<Guid>
{
    protected GuidStrongId(Guid value) : base(value) { }

    // Factory helpers
    public static T New<T>() where T : GuidStrongId => (T)Activator.CreateInstance(typeof(T), Guid.NewGuid())!;
    public static Result<T> FromString<T>(string value) where T : GuidStrongId
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<T>.Failure(Error.Validation($"{typeof(T).Name} cannot be empty"));

        return Guid.TryParse(value, out var g) && g != Guid.Empty
            ? Result<T>.Success((T)Activator.CreateInstance(typeof(T), g)!)
            : Result<T>.Failure(Error.Validation($"Invalid {typeof(T).Name} format"));
    }
}

/// <summary>
/// Int-based strong id.
/// </summary>
public abstract record IntStrongId : StrongId<int>
{
    protected IntStrongId(int value) : base(value)
    {
        if (value <= 0) throw new ArgumentException("Integer StrongId must be positive", nameof(value));
    }

    public static Result<T> FromString<T>(string s) where T : IntStrongId
    {
        if (!int.TryParse(s, out var v) || v <= 0)
            return Result<T>.Failure(Error.Validation($"Invalid {typeof(T).Name} format"));

        return Result<T>.Success((T)Activator.CreateInstance(typeof(T), v)!);
    }
}
