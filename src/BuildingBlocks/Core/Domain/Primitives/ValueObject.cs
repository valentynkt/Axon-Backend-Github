using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;

namespace BuildingBlocks.Core.Domain.Primitives;

/// <summary>
/// Base class for value objects following DDD principles and Epic 2 specifications.
/// Immutable and compared by value equality.
/// All value objects must implement validation and use Result-based factory patterns.
/// </summary>
public abstract record ValueObject
{
    /// <summary>
    /// Get components for equality comparison
    /// </summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();
    
    /// <summary>
    /// Validate the value object state
    /// All value objects must be valid upon creation
    /// </summary>
    public abstract Validation<Unit> Validate();
    
    /// <summary>
    /// Check if the value object is valid
    /// </summary>
    public bool IsValid => Validate().IsValid;
    
    /// <summary>
    /// Get validation errors
    /// </summary>
    public IReadOnlyList<Error> ValidationErrors => Validate().Errors;
    
    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);
    }
    
    public virtual bool Equals(ValueObject? other)
    {
        if (other is null || other.GetType() != GetType())
            return false;
            
        return GetEqualityComponents()
            .SequenceEqual(other.GetEqualityComponents());
    }
    
    /// <summary>
    /// OBSOLETE: Use Result-based factory methods instead of exception-throwing validation
    /// This method breaks the functional programming principles established in Epic 1
    /// </summary>
    [Obsolete("Use Result-based Create() factory methods. Exception-based validation violates functional principles.")]
    protected void EnsureValid()
    {
        var validation = Validate();
        if (validation.IsInvalid)
        {
            var message = string.Join("; ", validation.Errors.Select(e => e.Message));
            throw new InvalidOperationException($"Invalid {GetType().Name}: {message}");
        }
    }
}

/// <summary>
/// Base class for single-value value objects.
/// All value objects must be created via Result-based factory methods.
/// </summary>
/// <typeparam name="T">The type of the underlying value</typeparam>
public abstract record SingleValueObject<T> : ValueObject
{
    public T Value { get; }
    
    /// <summary>
    /// Protected constructor - use static Create methods for instantiation.
    /// Value objects should only be created through validated factory methods.
    /// </summary>
    protected SingleValueObject(T value)
    {
        Value = value;
        // No validation here - validation is responsibility of the factory method
        // This ensures consistent Result-based creation pattern
    }
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
    
    public override string ToString() => Value?.ToString() ?? string.Empty;
    
    public static implicit operator T(SingleValueObject<T> valueObject)
    {
        return valueObject.Value;
    }
}