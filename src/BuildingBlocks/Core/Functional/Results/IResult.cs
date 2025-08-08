namespace BuildingBlocks.Core.Functional.Results;

/// <summary>
/// Interface for Result types
/// </summary>
public interface IResult<T> : IResult
{
    T Value { get; }
    
    TResult Match<TResult>(
        Func<T, TResult> success,
        Func<Error, TResult> failure);
}

/// <summary>
/// Non-generic Result interface
/// </summary>
public interface IResult
{
    bool IsSuccess { get; }
    bool IsFailure { get; }
    Error Error { get; }
    
    TResult Match<TResult>(
        Func<TResult> success,
        Func<Error, TResult> failure);
}