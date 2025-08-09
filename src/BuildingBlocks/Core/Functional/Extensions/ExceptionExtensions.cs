using BuildingBlocks.Core.Diagnostics.Exceptions;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Functional.Extensions;

/// <summary>
/// Extension methods for converting domain exceptions to Results
/// </summary>
public static class ExceptionExtensions
{
    /// <summary>
    /// Convert DomainException to Result&lt;T&gt;
    /// </summary>
    public static Result<T> ToResult<T>(this DomainException exception)
    {
        return Result<T>.Failure(exception.Error);
    }
    
    /// <summary>
    /// Convert DomainException to Result
    /// </summary>
    public static Result ToResult(this DomainException exception)
    {
        return Result.Failure(exception.Error);
    }
    
    /// <summary>
    /// Execute operation and convert any DomainException to Result&lt;T&gt;
    /// </summary>
    public static Result<T> Try<T>(Func<T> operation)
    {
        try
        {
            var result = operation();
            return Result<T>.Success(result);
        }
        catch (DomainException ex)
        {
            return Result<T>.Failure(ex.Error);
        }
    }
    
    /// <summary>
    /// Execute operation and convert any DomainException to Result
    /// </summary>
    public static Result Try(Action operation)
    {
        try
        {
            operation();
            return Result.Success();
        }
        catch (DomainException ex)
        {
            return Result.Failure(ex.Error);
        }
    }
    
    /// <summary>
    /// Execute async operation and convert any DomainException to Result&lt;T&gt;
    /// </summary>
    public static async Task<Result<T>> TryAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            var result = await operation().ConfigureAwait(false);
            return Result<T>.Success(result);
        }
        catch (DomainException ex)
        {
            return Result<T>.Failure(ex.Error);
        }
    }
    
    /// <summary>
    /// Execute async operation and convert any DomainException to Result
    /// </summary>
    public static async Task<Result> TryAsync(Func<Task> operation)
    {
        try
        {
            await operation().ConfigureAwait(false);
            return Result.Success();
        }
        catch (DomainException ex)
        {
            return Result.Failure(ex.Error);
        }
    }
}
