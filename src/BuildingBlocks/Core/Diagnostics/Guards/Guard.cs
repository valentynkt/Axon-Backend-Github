using System.Numerics;
using System.Runtime.CompilerServices;

namespace BuildingBlocks.Core.Diagnostics.Guards;

/// <summary>
/// Provides guard clauses for defensive programming with automatic parameter name resolution.
/// </summary>
public static class Guard
{
    /// <summary>
    /// Start fluent validation for a value
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GuardClause<T> Against<T>(
        T value, 
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        return new GuardClause<T>(value, parameterName ?? "parameter");
    }
    
    /// <summary>
    /// Guard against null values
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T AgainstNull<T>(
        T? value, 
        [CallerArgumentExpression(nameof(value))] string? parameterName = null) 
        where T : class
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName, $"Parameter '{parameterName}' cannot be null");
        }
        
        return value;
    }
    
    /// <summary>
    /// Guard against null or empty strings
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string AgainstNullOrEmpty(
        string? value, 
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException($"Parameter '{parameterName}' cannot be null or empty", parameterName);
        }
        
        return value;
    }    
    /// <summary>
    /// Guard against null, empty, or whitespace strings
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string AgainstNullOrWhiteSpace(
        string? value, 
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Parameter '{parameterName}' cannot be null, empty, or whitespace", parameterName);
        }
        
        return value;
    }
    
    /// <summary>
    /// Guard against empty collections
    /// </summary>
    public static IEnumerable<T> AgainstEmpty<T>(
        IEnumerable<T>? collection, 
        [CallerArgumentExpression(nameof(collection))] string? parameterName = null)
    {
        if (collection is null)
        {
            throw new ArgumentNullException(parameterName, $"Parameter '{parameterName}' cannot be null");
        }
        
        if (!collection.Any())
        {
            throw new ArgumentException($"Parameter '{parameterName}' cannot be empty", parameterName);
        }
        
        return collection;
    }
    
    /// <summary>
    /// Guard against negative numbers
    /// </summary>
    public static T AgainstNegative<T>(
        T value, 
        [CallerArgumentExpression(nameof(value))] string? parameterName = null) 
        where T : INumber<T>
    {
        if (value < T.Zero)
        {
            throw new ArgumentOutOfRangeException(
                parameterName, 
                value, 
                $"Parameter '{parameterName}' cannot be negative");
        }
        
        return value;
    }    
    /// <summary>
    /// Guard against zero values
    /// </summary>
    public static T AgainstZero<T>(
        T value, 
        [CallerArgumentExpression(nameof(value))] string? parameterName = null) 
        where T : INumber<T>
    {
        if (value == T.Zero)
        {
            throw new ArgumentOutOfRangeException(
                parameterName, 
                value, 
                $"Parameter '{parameterName}' cannot be zero");
        }
        
        return value;
    }
    
    /// <summary>
    /// Guard against values outside of range
    /// </summary>
    public static T AgainstOutOfRange<T>(
        T value, T min, T max, 
        [CallerArgumentExpression(nameof(value))] string? parameterName = null) 
        where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName, 
                value, 
                $"Parameter '{parameterName}' must be between {min} and {max}");
        }
        
        return value;
    }
    
    /// <summary>
    /// Guard against condition being true
    /// </summary>
    public static T Against<T>(
        T value, 
        bool condition, 
        string message, 
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        if (condition)
        {
            throw new ArgumentException(message, parameterName);
        }
        
        return value;
    }    
    /// <summary>
    /// Guard against predicate being true
    /// </summary>
    public static T Against<T>(
        T value, 
        Func<T, bool> predicate, 
        string message, 
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        
        if (predicate(value))
        {
            throw new ArgumentException(message, parameterName);
        }
        
        return value;
    }
}