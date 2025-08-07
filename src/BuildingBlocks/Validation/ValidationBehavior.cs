using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional;

namespace BuildingBlocks.Validation;

/// <summary>
/// Legacy validation behavior - prefer ResultValidationBehavior for new implementations.
/// This behavior throws exceptions for validation failures instead of using Result pattern.
/// Maintained for backward compatibility with existing exception-based handlers.
/// </summary>
[Obsolete("Use ResultValidationBehavior for Result-aware validation. This will be removed in future versions.")]
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class, IAxonRequest<TResponse>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

    public ValidationBehavior(
        IServiceProvider serviceProvider, 
        ILogger<ValidationBehavior<TRequest, TResponse>> logger)
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

        var validator = _serviceProvider.GetService<IValidator<TRequest>>();
        if (validator is null)
        {
            _logger.LogDebug("No validator found for {RequestType}, proceeding without validation", 
                typeof(TRequest).Name);
            return await next(cancellationToken);
        }

        // Check if response type is Result-based and recommend the new behavior
        if (IsResultBasedResponse<TResponse>())
        {
            _logger.LogWarning(
                "Using legacy ValidationBehavior with Result-based response type {ResponseType}. " +
                "Consider migrating to ResultValidationBehavior for better Result pattern integration.",
                typeof(TResponse).Name);
        }

        _logger.LogDebug("Validating request {RequestType} with legacy ValidationBehavior", 
            typeof(TRequest).Name);

        // Use the existing extension method that throws exceptions
        await validator.HandleValidationAsync(request, cancellationToken);

        return await next(cancellationToken);
    }

    /// <summary>
    /// Determines if the response type is Result-based.
    /// </summary>
    private static bool IsResultBasedResponse<T>()
    {
        var responseType = typeof(T);
        
        // Check for Result
        if (responseType == typeof(Result))
            return true;

        // Check for Result<T>
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
            return true;

        return false;
    }
}