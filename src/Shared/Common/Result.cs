namespace Axon.Shared.Common;

/// <summary>
/// Represents the result of an operation that can either succeed or fail
/// </summary>
public readonly record struct Result
{
    private readonly Error? _error;
    
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error => IsFailure ? _error! : throw new InvalidOperationException("Cannot access Error when Result is successful");

    private Result(bool isSuccess, Error? error = null)
    {
        IsSuccess = isSuccess;
        _error = error;
    }

    public static Result Success() => new(true);
    public static Result Failure(Error error) => new(false, error);
    
    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail
/// </summary>
/// <typeparam name="T">The type of the success value</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Result pattern requires static factory methods")]
public readonly record struct Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;
    
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Cannot access Value when Result is failed");
    public Error Error => IsFailure ? _error! : throw new InvalidOperationException("Cannot access Error when Result is successful");

    private Result(bool isSuccess, T? value = default, Error? error = null)
    {
        IsSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    public static Result<T> Success(T value) => new(true, value);
    public static Result<T> Failure(Error error) => new(false, default, error);
    
    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
    public static implicit operator Result(Result<T> result) => 
        result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
}