using System.Collections;
using System.Runtime.CompilerServices;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Diagnostics.Exceptions;

namespace BuildingBlocks.Core.Diagnostics.Guards;

/// <summary>
/// Fluent API for guard clause validations that integrates with the Error system.
/// </summary>
/// <typeparam name="T">The type of value being validated</typeparam>
public sealed class GuardClause<T>
{
    private readonly T _value;
    private readonly string _parameterName;
    
    internal GuardClause(T value, string parameterName)
    {
        _value = value;
        _parameterName = parameterName;
    }
    
    /// <summary>
    /// Validates that the value is null. This check works for both reference types and nullable value types.
    /// </summary>
    public GuardClause<T> Null()
    {
        if (_value is not null)
        {
            GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be null.", _parameterName));
        }
        return this;
    }
    
    /// <summary>
    /// Validates that the value is not null. This check works for both reference types and nullable value types.
    /// </summary>
    public GuardClause<T> NotNull()
    {
        if (_value is null)
        {
            GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' cannot be null.", _parameterName));
        }
        return this;
    }
    
    /// <summary>
    /// Validates that the value is empty (for strings or collections).
    /// </summary>
    public GuardClause<T> Empty()
    {
        switch (_value)
        {
            case string { Length: > 0 }:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be empty.", _parameterName));
                break;
            case IEnumerable<object> enumerable when enumerable.Any():
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be empty.", _parameterName));
                break;
        }
        return this;
    }
    
    /// <summary>
    /// Validates that the value is not empty (for strings or collections).
    /// </summary>
    public GuardClause<T> NotEmpty()
    {
        switch (_value)
        {
            case string { Length: 0 }:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' cannot be empty.", _parameterName));
                break;
            case IEnumerable<object> enumerable when !enumerable.Any():
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' cannot be empty.", _parameterName));
                break;
        }
        return this;
    }
    
    /// <summary>
    /// Validates that the string value is not whitespace.
    /// </summary>
    public GuardClause<T> NotWhiteSpace()
    {
        if (_value is string str && string.IsNullOrWhiteSpace(str))
        {
            GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' cannot be whitespace.", _parameterName));
        }
        return this;
    }
    
    /// <summary>
    /// Validates that the string value is whitespace.
    /// </summary>
    public GuardClause<T> WhiteSpace()
    {
        if (_value is string str && !string.IsNullOrWhiteSpace(str))
        {
            GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be whitespace.", _parameterName));
        }
        return this;
    }
    
    /// <summary>
    /// Validates that the string has a minimum length.
    /// </summary>
    public GuardClause<T> MinLength(int minLength)
    {
        if (_value is string str && str.Length < minLength)
        {
            GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must have at least {minLength} characters but has {str.Length}.", _parameterName));
        }
        return this;
    }
    
    /// <summary>
    /// Validates that the string has a maximum length.
    /// </summary>
    public GuardClause<T> MaxLength(int maxLength)
    {
        if (_value is string str && str.Length > maxLength)
        {
            GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must have at most {maxLength} characters but has {str.Length}.", _parameterName));
        }
        return this;
    }
    
    /// <summary>
    /// Validates that the string matches the specified regex pattern.
    /// </summary>
    public GuardClause<T> Matches(string pattern)
    {
        if (_value is string str && !System.Text.RegularExpressions.Regex.IsMatch(str, pattern))
        {
            GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' does not match the required pattern.", _parameterName));
        }
        return this;
    }    
    /// <summary>
    /// Validates that the numeric value is not negative.
    /// </summary>
    public GuardClause<T> NotNegative()
    {
        switch (_value)
        {
            case int i when i < 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' cannot be negative.", _parameterName));
                break;
            case long l when l < 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' cannot be negative.", _parameterName));
                break;
            case decimal d when d < 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' cannot be negative.", _parameterName));
                break;
            case double d when d < 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' cannot be negative.", _parameterName));
                break;
            case float f when f < 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' cannot be negative.", _parameterName));
                break;
        }
        return this;
    }
    
    /// <summary>
    /// Validates that the numeric value is negative.
    /// </summary>
    public GuardClause<T> Negative()
    {
        switch (_value)
        {
            case int i when i >= 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be negative.", _parameterName));
                break;
            case long l when l >= 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be negative.", _parameterName));
                break;
            case decimal d when d >= 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be negative.", _parameterName));
                break;
            case double d when d >= 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be negative.", _parameterName));
                break;
            case float f when f >= 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be negative.", _parameterName));
                break;
        }
        return this;
    }
    
    /// <summary>
    /// Validates that the numeric value is not zero.
    /// </summary>
    public GuardClause<T> NotZero()
    {
        switch (_value)
        {
            case int i when i == 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' cannot be zero.", _parameterName));
                break;
            case long l when l == 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' cannot be zero.", _parameterName));
                break;
            case decimal d when d == 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' cannot be zero.", _parameterName));
                break;
            case double d when Math.Abs(d) < double.Epsilon:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' cannot be zero.", _parameterName));
                break;
            case float f when Math.Abs(f) < float.Epsilon:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' cannot be zero.", _parameterName));
                break;
        }
        return this;
    }    
    /// <summary>
    /// Validates that the numeric value is zero.
    /// </summary>
    public GuardClause<T> Zero()
    {
        switch (_value)
        {
            case int i when i != 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be zero.", _parameterName));
                break;
            case long l when l != 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be zero.", _parameterName));
                break;
            case decimal d when d != 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be zero.", _parameterName));
                break;
            case double d when Math.Abs(d) >= double.Epsilon:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be zero.", _parameterName));
                break;
            case float f when Math.Abs(f) >= float.Epsilon:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be zero.", _parameterName));
                break;
        }
        return this;
    }
    
    /// <summary>
    /// Validates that the numeric value is positive.
    /// </summary>
    public GuardClause<T> Positive()
    {
        switch (_value)
        {
            case int i when i <= 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be positive.", _parameterName));
                break;
            case long l when l <= 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be positive.", _parameterName));
                break;
            case decimal d when d <= 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be positive.", _parameterName));
                break;
            case double d when d <= 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be positive.", _parameterName));
                break;
            case float f when f <= 0:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be positive.", _parameterName));
                break;
        }
        return this;
    }    
    /// <summary>
    /// Validates that the value is greater than the specified minimum.
    /// </summary>
    public GuardClause<T> GreaterThan<TComparable>(TComparable minimum) where TComparable : IComparable<TComparable>
    {
        if (_value is TComparable comparable && comparable.CompareTo(minimum) <= 0)
        {
            GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be greater than {minimum}.", _parameterName));
        }
        return this;
    }
    
    /// <summary>
    /// Validates that the value is greater than or equal to the specified minimum.
    /// </summary>
    public GuardClause<T> GreaterThanOrEqual<TComparable>(TComparable minimum) where TComparable : IComparable<TComparable>
    {
        if (_value is TComparable comparable && comparable.CompareTo(minimum) < 0)
        {
            GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be greater than or equal to {minimum}.", _parameterName));
        }
        return this;
    }
    
    /// <summary>
    /// Validates that the value is less than the specified maximum.
    /// </summary>
    public GuardClause<T> LessThan<TComparable>(TComparable maximum) where TComparable : IComparable<TComparable>
    {
        if (_value is TComparable comparable && comparable.CompareTo(maximum) >= 0)
        {
            GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be less than {maximum}.", _parameterName));
        }
        return this;
    }
    
    /// <summary>
    /// Validates that the value is less than or equal to the specified maximum.
    /// </summary>
    public GuardClause<T> LessThanOrEqual<TComparable>(TComparable maximum) where TComparable : IComparable<TComparable>
    {
        if (_value is TComparable comparable && comparable.CompareTo(maximum) > 0)
        {
            GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be less than or equal to {maximum}.", _parameterName));
        }
        return this;
    }    
    /// <summary>
    /// Validates that the value is within the specified range (inclusive).
    /// </summary>
    public GuardClause<T> InRange<TComparable>(TComparable minimum, TComparable maximum) where TComparable : IComparable<TComparable>
    {
        if (_value is TComparable comparable && (comparable.CompareTo(minimum) < 0 || comparable.CompareTo(maximum) > 0))
        {
            GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must be between {minimum} and {maximum}.", _parameterName));
        }
        return this;
    }
    
    /// <summary>
    /// Validates that the collection has a minimum count.
    /// </summary>
    public GuardClause<T> MinCount(int minCount)
    {
        if (_value is IEnumerable enumerable)
        {
            var count = enumerable.Cast<object>().Count();
            if (count < minCount)
            {
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must have at least {minCount} items but has {count}.", _parameterName));
            }
        }
        return this;
    }
    
    /// <summary>
    /// Validates that the collection has a maximum count.
    /// </summary>
    public GuardClause<T> MaxCount(int maxCount)
    {
        if (_value is IEnumerable enumerable)
        {
            var count = enumerable.Cast<object>().Count();
            if (count > maxCount)
            {
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' must have at most {maxCount} items but has {count}.", _parameterName));
            }
        }
        return this;
    }    
    /// <summary>
    /// Validates that the date is not in the future.
    /// </summary>
    public GuardClause<T> NotInFuture()
    {
        switch (_value)
        {
            case DateTime dateTime when dateTime > DateTime.UtcNow:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' cannot be in the future.", _parameterName));
                break;
            case DateTimeOffset dateTimeOffset when dateTimeOffset > DateTimeOffset.UtcNow:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' cannot be in the future.", _parameterName));
                break;
            case DateOnly dateOnly when dateOnly > DateOnly.FromDateTime(DateTime.UtcNow):
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' cannot be in the future.", _parameterName));
                break;
        }
        return this;
    }
    
    /// <summary>
    /// Validates that the date is not in the past.
    /// </summary>
    public GuardClause<T> NotInPast()
    {
        switch (_value)
        {
            case DateTime dateTime when dateTime < DateTime.UtcNow:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' cannot be in the past.", _parameterName));
                break;
            case DateTimeOffset dateTimeOffset when dateTimeOffset < DateTimeOffset.UtcNow:
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' cannot be in the past.", _parameterName));
                break;
            case DateOnly dateOnly when dateOnly < DateOnly.FromDateTime(DateTime.UtcNow):
                GuardClause<T>.ThrowException(Error.Validation($"'{_parameterName}' cannot be in the past.", _parameterName));
                break;
        }
        return this;
    }    
    /// <summary>
    /// Validates with a custom predicate.
    /// </summary>
    public GuardClause<T> Must(Func<T, bool> predicate, string? customMessage = null)
    {
        if (!predicate(_value))
        {
            var message = customMessage ?? $"'{_parameterName}' does not satisfy the required condition.";
            GuardClause<T>.ThrowException(Error.Validation(message, _parameterName));
        }
        return this;
    }
    
    /// <summary>
    /// Validates with a custom predicate that returns an Error.
    /// </summary>
    public GuardClause<T> Must(Func<T, Error?> validator)
    {
        var error = validator(_value);
        if (error is not null)
        {
            GuardClause<T>.ThrowException(error);
        }
        return this;
    }
    
    /// <summary>
    /// Returns the validated value.
    /// </summary>
    public T Value => _value;
    
    /// <summary>
    /// Implicitly converts the guard clause to its validated value.
    /// </summary>
    public static implicit operator T(GuardClause<T> guardClause) => guardClause._value;
    
    private static void ThrowException(Error error)
    {
        throw new ValidationException(error);
    }
}