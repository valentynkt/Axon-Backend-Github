using BuildingBlocks.Core.Domain.CQRS;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Epic 2 + Epic 5 integration behavior for domain validation.
/// Executes domain business rules validation for commands that implement DomainCommandBase.
/// Runs after structural validation (ValidationBehavior) but before business logic execution.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public sealed class DomainValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : notnull
{
    private readonly ILogger<DomainValidationBehavior<TRequest, TResponse>> _logger;

    public DomainValidationBehavior(ILogger<DomainValidationBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        // Only validate domain commands
        if (request is not DomainCommandBase domainCommand)
        {
            return await next(cancellationToken);
        }

        _logger.LogDebug(
            "Executing domain validation for {CommandType} on aggregate {AggregateType}",
            request.GetType().Name,
            domainCommand.GetAggregateType().Name);

        // Execute domain business rules validation
        var validation = domainCommand.ValidateDomainRules();
        
        if (validation.IsInvalid)
        {
            _logger.LogWarning(
                "Domain validation failed for {CommandType}: {Errors}",
                request.GetType().Name,
                string.Join(", ", validation.Errors.Select(e => $"{e.Code}: {e.Message}")));

            // For Result-based responses, convert validation errors
            if (typeof(TResponse).IsGenericType && 
                typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
            {
                var resultType = typeof(TResponse).GetGenericArguments()[0];
                var failureMethod = typeof(Result<>)
                    .MakeGenericType(resultType)
                    .GetMethod(nameof(Result<Unit>.Failure), new[] { typeof(Error) });

                var error = Error.Multiple(validation.Errors.ToArray());
                var failureResult = failureMethod!.Invoke(null, new object[] { error });
                
                return (TResponse)failureResult!;
            }

            // For non-Result responses, throw validation exception
            throw new DomainValidationException(
                "Domain validation failed", 
                validation.Errors.ToArray());
        }

        _logger.LogDebug(
            "Domain validation passed for {CommandType}",
            request.GetType().Name);

        return await next(cancellationToken);
    }
}

/// <summary>
/// Exception thrown when domain validation fails for non-Result responses.
/// </summary>
public sealed class DomainValidationException : Exception
{
    public Error[] ValidationErrors { get; }

    public DomainValidationException(string message, Error[] validationErrors) 
        : base(message)
    {
        ValidationErrors = validationErrors;
    }

    public DomainValidationException(string message, Error[] validationErrors, Exception innerException) 
        : base(message, innerException)
    {
        ValidationErrors = validationErrors;
    }
}