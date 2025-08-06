using System.Diagnostics.CodeAnalysis;

#pragma warning disable CA1031 // Do not catch general exception types - intentional for Try methods

namespace BuildingBlocks.Core.Results;

/// <summary>
/// Represents the result of an operation that can either succeed or fail.
/// Follows functional programming patterns with method chaining and strong type safety.
/// Inspired by FluentResults and TypeScript Result patterns.
/// </summary>
public readonly record struct Result
{
    private readonly Error? _error;
    
    /// <summary>
    /// Indicates whether the operation was successful
    /// </summary>
    public bool IsSuccess { get; }
    
    /// <summary>
    /// Indicates whether the operation failed
    /// </summary>
    public bool IsFailure => !IsSuccess;
    
    /// <summary>
    /// Gets the error if the operation failed
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Error on a successful result</exception>
    public Error Error => IsFailure ? _error! : throw new InvalidOperationException("Cannot access Error when Result is successful");

    private Result(bool isSuccess, Error? error = null)
    {
        IsSuccess = isSuccess;
        _error = error;
    }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static Result Success() => new(true);
    
    /// <summary>
    /// Creates a failed result with the specified error
    /// </summary>
    public static Result Failure(Error error) => new(false, error);
    
    /// <summary>
    /// Creates a successful result with a value
    /// </summary>
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    
    /// <summary>
    /// Creates a failed result with a value type
    /// </summary>
    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);

    /// <summary>
    /// Executes a function and wraps potential exceptions in a Result
    /// </summary>
    public static Result Try(Action action, Func<System.Exception, Error>? errorFactory = null)
    {
        try
        {
            action();
            return Success();
        }
        catch (System.Exception ex)
        {
            var error = errorFactory?.Invoke(ex) ?? Error.InternalError(ex.Message, innerException: ex);
            return Failure(error);
        }
    }

    /// <summary>
    /// Executes a function and wraps potential exceptions in a Result with value
    /// </summary>
    public static Result<T> Try<T>(Func<T> func, Func<System.Exception, Error>? errorFactory = null)
    {
        try
        {
            var value = func();
            return Result<T>.Success(value);
        }
        catch (System.Exception ex)
        {
            var error = errorFactory?.Invoke(ex) ?? Error.InternalError(ex.Message, innerException: ex);
            return Result<T>.Failure(error);
        }
    }

    /// <summary>
    /// Executes an async function and wraps potential exceptions in a Result
    /// </summary>
    public static async Task<Result> TryAsync(Func<Task> func, Func<System.Exception, Error>? errorFactory = null)
    {
        try
        {
            await func();
            return Success();
        }
        catch (System.Exception ex)
        {
            var error = errorFactory?.Invoke(ex) ?? Error.InternalError(ex.Message, innerException: ex);
            return Failure(error);
        }
    }

    /// <summary>
    /// Executes an async function and wraps potential exceptions in a Result with value
    /// </summary>
    public static async Task<Result<T>> TryAsync<T>(Func<Task<T>> func, Func<System.Exception, Error>? errorFactory = null)
    {
        try
        {
            var value = await func();
            return Result<T>.Success(value);
        }
        catch (System.Exception ex)
        {
            var error = errorFactory?.Invoke(ex) ?? Error.InternalError(ex.Message, innerException: ex);
            return Result<T>.Failure(error);
        }
    }

    /// <summary>
    /// Combines multiple results. Returns success only if all results are successful.
    /// </summary>
    public static Result Combine(params Result[] results)
    {
        var failures = results.Where(r => r.IsFailure).ToList();
        if (failures.Count == 0)
            return Success();

        // For simplicity, return the first error. In advanced scenarios, you might want to aggregate errors.
        return Failure(failures.First().Error);
    }

    /// <summary>
    /// Combines multiple results with values. Returns success only if all results are successful.
    /// </summary>
    public static Result<T[]> Combine<T>(params Result<T>[] results)
    {
        var failures = results.Where(r => r.IsFailure).ToList();
        if (failures.Count == 0)
        {
            var values = results.Select(r => r.Value).ToArray();
            return Result<T[]>.Success(values);
        }

        return Result<T[]>.Failure(failures.First().Error);
    }

    /// <summary>
    /// Executes an action if the result is successful
    /// </summary>
    public Result OnSuccess(Action action)
    {
        if (IsSuccess)
            action();
        return this;
    }

    /// <summary>
    /// Executes an action if the result is failed
    /// </summary>
    public Result OnFailure(Action<Error> action)
    {
        if (IsFailure)
            action(Error);
        return this;
    }

    /// <summary>
    /// Transforms the result based on success or failure
    /// </summary>
    public T Match<T>(Func<T> onSuccess, Func<Error, T> onFailure)
    {
        return IsSuccess ? onSuccess() : onFailure(Error);
    }

    /// <summary>
    /// Implicit conversion from Error to Result
    /// </summary>
    public static implicit operator Result(Error error) => Failure(error);

    public override string ToString() => IsSuccess ? "Success" : $"Failure: {Error}";
}

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail.
/// Supports method chaining and functional programming patterns.
/// </summary>
/// <typeparam name="T">The type of the success value</typeparam>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Result pattern requires static factory methods")]
public readonly record struct Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;
    
    /// <summary>
    /// Indicates whether the operation was successful
    /// </summary>
    public bool IsSuccess { get; }
    
    /// <summary>
    /// Indicates whether the operation failed
    /// </summary>
    public bool IsFailure => !IsSuccess;
    
    /// <summary>
    /// Gets the value if the operation was successful
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Value on a failed result</exception>
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Cannot access Value when Result is failed");
    
    /// <summary>
    /// Gets the error if the operation failed
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Error on a successful result</exception>
    public Error Error => IsFailure ? _error! : throw new InvalidOperationException("Cannot access Error when Result is successful");

    private Result(bool isSuccess, T? value = default, Error? error = null)
    {
        IsSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    /// <summary>
    /// Creates a successful result with the specified value
    /// </summary>
    public static Result<T> Success(T value) => new(true, value);
    
    /// <summary>
    /// Creates a failed result with the specified error
    /// </summary>
    public static Result<T> Failure(Error error) => new(false, default, error);

    /// <summary>
    /// Gets the value if successful, otherwise returns the default value
    /// </summary>
    public T? ValueOrDefault => IsSuccess ? _value : default;

    /// <summary>
    /// Gets the value if successful, otherwise returns the specified default value
    /// </summary>
    public T GetValueOrDefault(T defaultValue) => IsSuccess ? _value! : defaultValue;

    /// <summary>
    /// Gets the value if successful, otherwise computes and returns a default value
    /// </summary>
    public T GetValueOrDefault(Func<Error, T> defaultValueFactory) => 
        IsSuccess ? _value! : defaultValueFactory(Error);

    /// <summary>
    /// Transforms the value if the result is successful
    /// </summary>
    public Result<TOutput> Map<TOutput>(Func<T, TOutput> transform)
    {
        return IsSuccess ? Result<TOutput>.Success(transform(_value!)) : Result<TOutput>.Failure(Error);
    }

    /// <summary>
    /// Transforms the value if the result is successful, allowing the transform to return a Result
    /// </summary>
    public Result<TOutput> Bind<TOutput>(Func<T, Result<TOutput>> transform)
    {
        return IsSuccess ? transform(_value!) : Result<TOutput>.Failure(Error);
    }

    /// <summary>
    /// Transforms the error if the result is failed
    /// </summary>
    public Result<T> MapError(Func<Error, Error> transform)
    {
        return IsFailure ? Failure(transform(Error)) : this;
    }

    /// <summary>
    /// Provides a fallback value or operation if the result is failed
    /// </summary>
    public Result<T> OrElse(Func<Error, Result<T>> fallback)
    {
        return IsFailure ? fallback(Error) : this;
    }

    /// <summary>
    /// Provides a fallback value if the result is failed
    /// </summary>
    public Result<T> OrElse(T fallbackValue)
    {
        return IsFailure ? Success(fallbackValue) : this;
    }

    /// <summary>
    /// Executes an action if the result is successful
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess)
            action(_value!);
        return this;
    }

    /// <summary>
    /// Executes an action if the result is failed
    /// </summary>
    public Result<T> OnFailure(Action<Error> action)
    {
        if (IsFailure)
            action(Error);
        return this;
    }

    /// <summary>
    /// Transforms the result based on success or failure
    /// </summary>
    public TOutput Match<TOutput>(Func<T, TOutput> onSuccess, Func<Error, TOutput> onFailure)
    {
        return IsSuccess ? onSuccess(_value!) : onFailure(Error);
    }

    /// <summary>
    /// Converts a Result&lt;T&gt; to a non-generic Result
    /// </summary>
    public Result ToResult() => IsSuccess ? Result.Success() : Result.Failure(Error);

    /// <summary>
    /// Implicit conversion from value to successful Result
    /// </summary>
    public static implicit operator Result<T>(T value) => Success(value);
    
    /// <summary>
    /// Implicit conversion from Error to failed Result
    /// </summary>
    public static implicit operator Result<T>(Error error) => Failure(error);
    
    /// <summary>
    /// Implicit conversion from Result&lt;T&gt; to non-generic Result
    /// </summary>
    public static implicit operator Result(Result<T> result) => 
        result.IsSuccess ? Result.Success() : Result.Failure(result.Error);

    public override string ToString() => IsSuccess ? $"Success: {_value}" : $"Failure: {Error}";
}