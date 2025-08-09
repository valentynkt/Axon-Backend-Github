using System.Data.Common;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security;
using System.Text.Json;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Diagnostics.Exceptions;
using BuildingBlocks.Core.Functional.Results;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Core.Functional.Exceptions;

/// <summary>
/// Provides bidirectional mapping between Result pattern and traditional .NET exceptions.
/// Enables gradual migration and integration with legacy exception-based code.
/// Maintains error context and type information across conversions.
/// </summary>
public static class ResultExceptionMapper
{
    /// <summary>
    /// Converts a Result to an exception if it represents a failure.
    /// Returns null for successful results.
    /// </summary>
    /// <param name="result">The result to convert</param>
    /// <returns>An exception representing the failure, or null for success</returns>
    public static Exception? ToException(this Result result)
    {
        if (result.IsSuccess)
            return null;

        return CreateExceptionFromError(result.Error);
    }

    /// <summary>
    /// Converts a Result{T} to an exception if it represents a failure.
    /// Returns null for successful results.
    /// </summary>
    /// <typeparam name="T">The result value type</typeparam>
    /// <param name="result">The result to convert</param>
    /// <returns>An exception representing the failure, or null for success</returns>
    public static Exception? ToException<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return null;

        return CreateExceptionFromError(result.Error);
    }

    /// <summary>
    /// Throws an exception if the Result represents a failure.
    /// Does nothing for successful results.
    /// </summary>
    /// <param name="result">The result to check</param>
    /// <exception cref="Exception">The exception representing the failure</exception>
    public static void ThrowIfFailure(this Result result)
    {
        var exception = result.ToException();
        if (exception != null)
            throw exception;
    }

    /// <summary>
    /// Throws an exception if the Result{T} represents a failure.
    /// Does nothing for successful results.
    /// </summary>
    /// <typeparam name="T">The result value type</typeparam>
    /// <param name="result">The result to check</param>
    /// <exception cref="Exception">The exception representing the failure</exception>
    public static void ThrowIfFailure<T>(this Result<T> result)
    {
        var exception = result.ToException();
        if (exception != null)
            throw exception;
    }

    /// <summary>
    /// Converts an exception to a failed Result.
    /// Preserves exception context and categorizes by exception type.
    /// </summary>
    /// <param name="exception">The exception to convert</param>
    /// <returns>A failed Result containing the error</returns>
    public static Result ToResult(this Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        
        var error = CreateErrorFromException(exception);
        return Result.Failure(error);
    }

    /// <summary>
    /// Converts an exception to a failed Result{T}.
    /// Preserves exception context and categorizes by exception type.
    /// </summary>
    /// <typeparam name="T">The result value type</typeparam>
    /// <param name="exception">The exception to convert</param>
    /// <returns>A failed Result{T} containing the error</returns>
    public static Result<T> ToResult<T>(this Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        
        var error = CreateErrorFromException(exception);
        return Result<T>.Failure(error);
    }

    /// <summary>
    /// Safely executes a function and converts any exceptions to Result pattern.
    /// Prevents exceptions from escaping and maintains railway-oriented programming.
    /// </summary>
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="function">The function to execute</param>
    /// <returns>A Result containing either the function result or the exception as an error</returns>
    public static Result<T> TryExecute<T>(Func<T> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        
        try
        {
            var result = function();
            return Result<T>.Success(result);
        }
        catch (Exception ex)
        {
            return ex.ToResult<T>();
        }
    }

    /// <summary>
    /// Safely executes an async function and converts any exceptions to Result pattern.
    /// Handles cancellation and other async-specific exceptions appropriately.
    /// </summary>
    /// <typeparam name="T">The return type</typeparam>
    /// <param name="function">The async function to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A Result containing either the function result or the exception as an error</returns>
    public static async Task<Result<T>> TryExecuteAsync<T>(
        Func<CancellationToken, Task<T>> function,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(function);
        
        try
        {
            var result = await function(cancellationToken).ConfigureAwait(false);
            return Result<T>.Success(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<T>.Failure(Error.Cancelled("Operation was cancelled"));
        }
        catch (Exception ex)
        {
            return ex.ToResult<T>();
        }
    }

    /// <summary>
    /// Safely executes an action and converts any exceptions to Result pattern.
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <returns>A Result indicating success or failure</returns>
    public static Result TryExecute(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        
        try
        {
            action();
            return Result.Success();
        }
        catch (Exception ex)
        {
            return ex.ToResult();
        }
    }

    /// <summary>
    /// Safely executes an async action and converts any exceptions to Result pattern.
    /// </summary>
    /// <param name="action">The async action to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A Result indicating success or failure</returns>
    public static async Task<Result> TryExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        
        try
        {
            await action(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure(Error.Cancelled("Operation was cancelled"));
        }
        catch (Exception ex)
        {
            return ex.ToResult();
        }
    }

    /// <summary>
    /// Creates an appropriate exception from an Error based on error type.
    /// Maps error types to corresponding .NET and domain exception types.
    /// Prefers domain exceptions when available for better error context.
    /// </summary>
    private static Exception CreateExceptionFromError(Error error)
    {
        var baseException = error.InnerException;

        return error.Type switch
        {
            // 4xx Client Errors - Use domain exceptions where appropriate
            ErrorType.Validation => new DomainException(error),
            ErrorType.NotFound => new DomainException(error),
            ErrorType.Conflict => new DomainException(error),
            ErrorType.BusinessRule => new DomainException(error),
            ErrorType.Unauthorized => new UnauthorizedAccessException(error.Message, baseException),
            ErrorType.Forbidden => new UnauthorizedAccessException(error.Message, baseException),
            ErrorType.PreconditionFailed => new InvalidOperationException(error.Message, baseException),
            ErrorType.RateLimit => new InvalidOperationException(error.Message, baseException),
            ErrorType.Cancelled => new OperationCanceledException(error.Message, baseException),
            ErrorType.Serialization => new JsonException(error.Message, baseException),
            ErrorType.Concurrency => new ConcurrencyException(error.Message),
            
            // 5xx Server Errors - Map to system exceptions
            ErrorType.Internal => new InvalidOperationException(error.Message, baseException),
            ErrorType.Configuration => new InvalidOperationException(error.Message, baseException),
            ErrorType.External => new HttpRequestException(error.Message, baseException),
            ErrorType.Network => baseException switch
            {
                SocketException => baseException,
                HttpRequestException => baseException,
                _ => new HttpRequestException(error.Message, baseException)
            },
            ErrorType.Timeout => new TimeoutException(error.Message, baseException),
            ErrorType.Unavailable => new InvalidOperationException(error.Message, baseException),
            ErrorType.Persistence => baseException switch
            {
                DbException => baseException,
                DbUpdateException => baseException,
                _ => new InvalidOperationException(error.Message, baseException)
            },
            
            // Domain/Composed Errors
            ErrorType.Aggregate => new DomainException(error),
            ErrorType.Security => new SecurityException(error.Message, baseException),
            
            // Fallback
            _ => new DomainException(error)
        };
    }

    /// <summary>
    /// Creates an Error from an exception with appropriate categorization.
    /// Leverages the comprehensive Error.FromException method for consistent mapping.
    /// Preserves round-trip consistency for domain exceptions.
    /// </summary>
    private static Error CreateErrorFromException(Exception exception)
    {
        // If it's already a DomainException, extract its Error directly
        if (exception is DomainException domainEx)
        {
            return domainEx.Error;
        }

        // If the exception already contains an Error (from our system), extract it
        if (exception.Data.Contains("AxonError") && exception.Data["AxonError"] is Error existingError)
        {
            return existingError;
        }

        // Leverage the comprehensive Error.FromException method which handles all exception types
        var error = Error.FromException(exception);

        // Add the error to exception data for round-trip consistency
        try
        {
            exception.Data["AxonError"] = error;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch
        {
            // Some exceptions don't support Data modification; ignore silently
        }
#pragma warning restore CA1031

        return error;
    }
}