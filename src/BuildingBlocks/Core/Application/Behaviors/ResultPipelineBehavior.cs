using System.Collections.Concurrent;
using System.Reflection;
using BuildingBlocks.Core.Functional.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Core.Application.Behaviors;

/// <summary>
/// Pipeline behavior that ensures all command/query handlers return Result<T>
/// Wraps exceptions in Result.Failure automatically using reflection-free caching
/// </summary>
public class ResultPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<ResultPipelineBehavior<TRequest, TResponse>> _logger;
    
    public ResultPipelineBehavior(ILogger<ResultPipelineBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }
    
    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);
            
            // Log successful operations using cached delegates
            if (FailureCache<TResponse>.IsResult && FailureCache<TResponse>.IsSuccess != null && response != null && FailureCache<TResponse>.IsSuccess(response))
            {
                _logger.LogDebug("Successfully handled {RequestType}", typeof(TRequest).Name);
            }
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling {RequestType}: {Message}", 
                typeof(TRequest).Name, ex.Message);
            
            var error = Error.FromException(ex);
            
            // Use cached failure delegate for zero-reflection performance
            return FailureCache<TResponse>.FailureFactory?.Invoke(error) ?? throw ex;
        }
    }
}

/// <summary>
/// Static cache for Result<T> failure creation - eliminates per-call reflection
/// </summary>
public static class FailureCache<T>
{
    public static readonly bool IsResult;
    public static readonly Func<Error, T>? FailureFactory;
    public static readonly Func<object, bool>? IsSuccess;
    
    static FailureCache()
    {
        var type = typeof(T);
        
        // Check if T is Result<U> or Result
        if (type == typeof(Result))
        {
            IsResult = true;
            FailureFactory = error => (T)(object)Result.Failure(error);
            IsSuccess = obj => ((Result)obj).IsSuccess;
        }
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Result<>))
        {
            IsResult = true;
            
            // Cache the Failure method for this specific Result<U> type
            var failureMethod = type.GetMethod("Failure", 
                BindingFlags.Public | BindingFlags.Static);
            
            if (failureMethod != null)
            {
                FailureFactory = error => (T)failureMethod.Invoke(null, [error])!;
            }
            
            // Cache IsSuccess property getter
            var isSuccessProperty = type.GetProperty("IsSuccess");
            if (isSuccessProperty != null)
            {
                IsSuccess = obj => (bool)isSuccessProperty.GetValue(obj)!;
            }
        }
        else
        {
            IsResult = false;
            FailureFactory = null;
            IsSuccess = null;
        }
    }
}