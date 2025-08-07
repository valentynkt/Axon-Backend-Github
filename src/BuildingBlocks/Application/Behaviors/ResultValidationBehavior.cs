using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Result-aware validation behavior for MediatR pipeline.
/// Converts FluentValidation failures to Result pattern errors instead of throwing exceptions.
/// Maintains railway-oriented programming flow by returning failed Results.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type - must be Result or Result{T}</typeparam>
public sealed class ResultValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class, IAxonRequest<TResponse>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ResultValidationBehavior<TRequest, TResponse>> _logger;

    public ResultValidationBehavior(
        IServiceProvider serviceProvider,
        ILogger<ResultValidationBehavior<TRequest, TResponse>> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        // Get validator if available
        var validator = _serviceProvider.GetService<IValidator<TRequest>>();
        if (validator is null)
        {
            _logger.LogDebug("No validator found for {RequestType}, proceeding without validation", 
                typeof(TRequest).Name);
            return await next(cancellationToken);
        }

        _logger.LogDebug("Validating request {RequestType} with RequestId {RequestId}", 
            typeof(TRequest).Name, request.RequestId);

        // Perform validation
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        
        if (validationResult.IsValid)
        {
            _logger.LogDebug("Request {RequestType} validation passed", typeof(TRequest).Name);
            return await next(cancellationToken);
        }

        // Convert validation failures to Result pattern
        _logger.LogWarning("Request {RequestType} validation failed with {ErrorCount} errors", 
            typeof(TRequest).Name, validationResult.Errors.Count);

        return CreateValidationFailureResult<TResponse>(validationResult);
    }

    /// <summary>
    /// Creates a failed Result from FluentValidation failures.
    /// Handles both Result and Result{T} response types.
    /// </summary>
    private static TResponse CreateValidationFailureResult<T>(FluentValidation.Results.ValidationResult validationResult)
    {
        var errors = validationResult.Errors
            .Select(failure => Error.Validation(
                failure.ErrorMessage, 
                failure.ErrorCode ?? "VALIDATION_ERROR",
                $"Property: {failure.PropertyName}, AttemptedValue: {failure.AttemptedValue}"))
            .ToArray();

        var aggregatedError = errors.Length == 1 
            ? errors[0]
            : Error.Aggregate(errors);

        // Handle Result{T} response types
        if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var resultType = typeof(T).GetGenericArguments()[0];
            var failureMethod = typeof(Result<>)
                .MakeGenericType(resultType)
                .GetMethod(nameof(Result<object>.Failure), new[] { typeof(Error) });
            
            var failedResult = failureMethod!.Invoke(null, new object[] { aggregatedError });
            return (T)failedResult!;
        }

        // Handle non-generic Result response type
        if (typeof(T) == typeof(Result))
        {
            var failedResult = Result.Failure(aggregatedError);
            return (T)(object)failedResult;
        }

        // If response type is not Result-based, we have a configuration issue
        throw new InvalidOperationException(
            $"ResultValidationBehavior can only be used with Result or Result<T> response types. " +
            $"Found: {typeof(T).Name}");
    }
}