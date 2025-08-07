using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Domain.Primitives;

/// <summary>
/// Base implementation for strongly-typed identifiers
/// Provides type safety, validation, and serialization support
/// </summary>
public abstract record StrongId<TPrimitive> : IStrongId<TPrimitive>, IComparable<StrongId<TPrimitive>>
    where TPrimitive : struct, IComparable<TPrimitive>, IEquatable<TPrimitive>
{
    public TPrimitive Value { get; }
    
    protected StrongId(TPrimitive value)
    {
        if (value.Equals(default(TPrimitive)))
            throw new ArgumentException($"StrongId value cannot be default({typeof(TPrimitive).Name})", nameof(value));
            
        Value = value;
    }
    
    #region IStrongId Implementation
    
    public object GetValue() => Value;
    public Type GetValueType() => typeof(TPrimitive);
    
    #endregion
    
    #region Comparison
    
    public int CompareTo(StrongId<TPrimitive>? other)
    {
        if (other is null) return 1;
        return Value.CompareTo(other.Value);
    }
    
    public static bool operator <(StrongId<TPrimitive> left, StrongId<TPrimitive> right)
        => left.CompareTo(right) < 0;
        
    public static bool operator >(StrongId<TPrimitive> left, StrongId<TPrimitive> right)
        => left.CompareTo(right) > 0;
        
    public static bool operator <=(StrongId<TPrimitive> left, StrongId<TPrimitive> right)
        => left.CompareTo(right) <= 0;
        
    public static bool operator >=(StrongId<TPrimitive> left, StrongId<TPrimitive> right)
        => left.CompareTo(right) >= 0;
    
    #endregion
    
    #region Conversion
    
    public static implicit operator TPrimitive(StrongId<TPrimitive> strongId) => strongId.Value;
    
    public override string ToString() => Value.ToString() ?? string.Empty;
    
    #endregion
}

/// <summary>
/// GUID-based strongly-typed identifier
/// </summary>
public abstract record GuidStrongId : StrongId<Guid>
{
    protected GuidStrongId(Guid value) : base(value) { }
    
    protected GuidStrongId() : base(Guid.NewGuid()) { }
    
    /// <summary>
    /// Create a new instance with a new GUID
    /// </summary>
    public static T New<T>() where T : GuidStrongId, new() => new();
    
    /// <summary>
    /// Create from string representation
    /// </summary>
    protected static Result<T> FromString<T>(string value, Func<Guid, T> factory)
        where T : GuidStrongId
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<T>.Failure(Error.Validation($"{typeof(T).Name} cannot be empty"));
            
        if (!Guid.TryParse(value, out var guid))
            return Result<T>.Failure(Error.Validation($"Invalid {typeof(T).Name} format"));
            
        if (guid == Guid.Empty)
            return Result<T>.Failure(Error.Validation($"{typeof(T).Name} cannot be empty GUID"));
            
        return Result<T>.Success(factory(guid));
    }
}

/// <summary>
/// Integer-based strongly-typed identifier
/// </summary>
public abstract record IntStrongId : StrongId<int>
{
    protected IntStrongId(int value) : base(value) 
    { 
        if (value <= 0)
            throw new ArgumentException("Integer StrongId must be positive", nameof(value));
    }
    
    /// <summary>
    /// Create from string representation
    /// </summary>
    protected static Result<T> FromString<T>(string value, Func<int, T> factory)
        where T : IntStrongId
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<T>.Failure(Error.Validation($"{typeof(T).Name} cannot be empty"));
            
        if (!int.TryParse(value, out var id))
            return Result<T>.Failure(Error.Validation($"Invalid {typeof(T).Name} format"));
            
        if (id <= 0)
            return Result<T>.Failure(Error.Validation($"{typeof(T).Name} must be positive"));
            
        return Result<T>.Success(factory(id));
    }
}