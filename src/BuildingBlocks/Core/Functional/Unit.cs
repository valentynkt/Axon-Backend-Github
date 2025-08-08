namespace BuildingBlocks.Core.Functional;

/// <summary>
/// Represents a value that carries no information - functional equivalent of void.
/// Used in Result<Unit> and Option<Unit> for operations that return no meaningful data.
/// Follows F#'s unit type pattern.
/// </summary>
public readonly record struct Unit : IComparable<Unit>, IEquatable<Unit>
{
    /// <summary>
    /// The single instance of Unit type
    /// </summary>
    public static readonly Unit Value = new();
    
    /// <summary>
    /// Default constructor creates the unit value
    /// </summary>
    public Unit() { }
    
    /// <summary>
    /// String representation
    /// </summary>
    public override string ToString() => "()";
    
    /// <summary>
    /// Comparison always returns equal
    /// </summary>
    public int CompareTo(Unit other) => 0;
    
    /// <summary>
    /// Equality always returns true
    /// </summary>
    public bool Equals(Unit other) => true;
    
    /// <summary>
    /// Hash code is constant
    /// </summary>
    public override int GetHashCode() => 0;
    
    /// <summary>
    /// Implicit conversion from any value to Unit (discards the value)
    /// </summary>
    public static implicit operator Unit(object? _) => Value;
    
    /// <summary>
    /// Task<Unit> factory for async operations that return no data
    /// </summary>
    public static Task<Unit> Task => System.Threading.Tasks.Task.FromResult(Value);
    
    /// <summary>
    /// ValueTask<Unit> factory for high-performance async operations
    /// </summary>
    public static ValueTask<Unit> ValueTask => System.Threading.Tasks.ValueTask.FromResult(Value);

    public static bool operator <(Unit left, Unit right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(Unit left, Unit right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(Unit left, Unit right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(Unit left, Unit right)
    {
        return left.CompareTo(right) >= 0;
    }
}

/// <summary>
/// Extension methods for Unit type
/// </summary>
public static class UnitExtensions
{
    /// <summary>
    /// Converts any value to Unit, discarding the original value
    /// </summary>
    public static Unit ToUnit<T>(this T _) => Unit.Value;
    
    /// <summary>
    /// Converts Task<T> to Task<Unit>, discarding the result
    /// </summary>
    public static async Task<Unit> ToUnit<T>(this Task<T> task)
    {
        await task.ConfigureAwait(false);
        return Unit.Value;
    }
    
    /// <summary>
    /// Converts ValueTask<T> to ValueTask<Unit>, discarding the result
    /// </summary>
    public static async ValueTask<Unit> ToUnit<T>(this ValueTask<T> task)
    {
        await task.ConfigureAwait(false);
        return Unit.Value;
    }
    
    /// <summary>
    /// Converts Task to Task<Unit>
    /// </summary>
    public static async Task<Unit> ToUnit(this Task task)
    {
        await task.ConfigureAwait(false);
        return Unit.Value;
    }
    
    /// <summary>
    /// Converts ValueTask to ValueTask<Unit>
    /// </summary>
    public static async ValueTask<Unit> ToUnit(this ValueTask task)
    {
        await task.ConfigureAwait(false);
        return Unit.Value;
    }
}