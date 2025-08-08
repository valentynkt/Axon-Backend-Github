using FluentValidation;
using MediatR;
using BuildingBlocks.Core.Domain.Integration;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Application.Validation;
using Microsoft.Extensions.Logging;
using IValidationContext = BuildingBlocks.Application.Validation.IValidationContext;
using Unit = BuildingBlocks.Core.Functional.Unit;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that provides comprehensive validation for commands and queries.
/// Integrates Epic 2 domain rules with Epic 5 pipeline behaviors for unified validation.
/// 
/// This behavior:
/// 1. Runs FluentValidation validators for structural validation
/// 2. Executes domain business rules for business logic validation  
/// 3. Aggregates all validation errors into structured responses
/// 4. Fails fast before expensive operations (caching, transactions)
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type (must be Result-based)</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResult
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    {
        _validators = validators ?? throw new ArgumentNullException(nameof(validators));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestType"] = typeof(TRequest).Name,
            ["ResponseType"] = typeof(TResponse).Name
        });

        _logger.LogDebug("Starting validation for {RequestType}", typeof(TRequest).Name);

        // Epic 04: Create validation context from request metadata if available
        ValidationContext? validationContext = null;
        if (request is IAxonRequest axonRequest)
        {
            validationContext = new ValidationContext(axonRequest);
            _logger.LogDebug("Created validation context with TraceId: {TraceId}, TenantId: {TenantId}", 
                validationContext.TraceId, validationContext.TenantId);
        }

        // 1. Structural validation using FluentValidation with context injection
        var structuralValidation = await ValidateStructureAsync(request, validationContext, cancellationToken);
        
        // 2. Domain business rules validation (Epic 2 integration)
        var domainValidation = await ValidateDomainRulesAsync(request, cancellationToken);
        
        // 3. Combine all validation results
        var combinedValidation = CombineValidations(structuralValidation, domainValidation);
        
        if (combinedValidation.IsInvalid)
        {
            _logger.LogWarning(
                "Validation failed for {RequestType} with {ErrorCount} errors: {Errors}",
                typeof(TRequest).Name,
                combinedValidation.Errors.Count,
                string.Join("; ", combinedValidation.Errors.Select(e => e.Message)));

            // Enhanced error creation with metadata context
            var groupedErrors = GroupValidationErrors(combinedValidation.Errors);
            var aggregatedError = CreateValidationErrorWithMetadata(groupedErrors, validationContext);
            
            return CreateFailureResponse<TResponse>(aggregatedError);
        }

        _logger.LogDebug("Validation successful for {RequestType}", typeof(TRequest).Name);
        
        // Validation passed, proceed to next behavior
        return await next(cancellationToken);
    }

    /// <summary>
    /// Perform structural validation using FluentValidation validators with Epic 04 context injection.
    /// </summary>
    private async Task<Validation<Unit>> ValidateStructureAsync(
        TRequest request, 
        IValidationContext? validationContext,
        CancellationToken cancellationToken)
    {
        var validators = _validators.ToList();
        if (validators.Count == 0)
        {
            return Validation<Unit>.Valid(Unit.Value);
        }

        _logger.LogDebug("Running {ValidatorCount} FluentValidation validators", validators.Count);

        // Epic 04: Inject validation context into metadata-aware validators
        var metadataValidators = 0;
        if (validationContext != null)
        {
            foreach (var validator in validators.OfType<IMetadataValidator>())
            {
                validator.SetContext(validationContext);
                metadataValidators++;
            }
            
            if (metadataValidators > 0)
            {
                _logger.LogDebug("Injected validation context into {MetadataValidatorCount} metadata-aware validators", 
                    metadataValidators);
            }
        }

        var errors = new List<Error>();

        foreach (var validator in validators)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            
            if (!validationResult.IsValid)
            {
                var validationErrors = validationResult.Errors
                    .Select(failure => Error.Validation(failure.ErrorMessage, failure.PropertyName))
                    .ToArray();
                    
                errors.AddRange(validationErrors);
            }
        }

        return errors.Count != 0
            ? Validation<Unit>.Invalid(errors)
            : Validation<Unit>.Valid(Unit.Value);
    }

    /// <summary>
    /// Perform domain business rules validation (Epic 2 integration).
    /// </summary>
    private async Task<Validation<Unit>> ValidateDomainRulesAsync(
        TRequest request, 
        CancellationToken cancellationToken)
    {
        // Check if request supports domain validation
        switch (request)
        {
            case IDomainValidatableAsync asyncValidatable:
                _logger.LogDebug("Running async domain business rules validation");
                return await asyncValidatable.ValidateDomainRulesAsync(cancellationToken);
                
            case IDomainValidatable validatable:
                _logger.LogDebug("Running sync domain business rules validation");
                return validatable.ValidateDomainRules();
                
            default:
                // No domain validation required
                return Validation<Unit>.Valid(Unit.Value);
        }
    }

    /// <summary>
    /// Combine structural and domain validation results.
    /// </summary>
    private static Validation<Unit> CombineValidations(
        Validation<Unit> structuralValidation,
        Validation<Unit> domainValidation)
    {
        var allErrors = new List<Error>();
        
        if (structuralValidation.IsInvalid)
        {
            allErrors.AddRange(structuralValidation.Errors);
        }
        
        if (domainValidation.IsInvalid)
        {
            allErrors.AddRange(domainValidation.Errors);
        }

        return allErrors.Count != 0
            ? Validation<Unit>.Invalid(allErrors)
            : Validation<Unit>.Valid(Unit.Value);
    }

    /// <summary>
    /// Group validation errors by property name for better client experience.
    /// This follows the Epic 5 specification for error aggregation.
    /// </summary>
    private static Error[] GroupValidationErrors(IReadOnlyList<Error> errors)
    {
        // Group errors by their code (which represents property name for validation errors)
        var groupedErrors = errors
            .Where(e => e.Type == ErrorType.Validation)
            .GroupBy(e => e.Code)
            .Select(group => 
            {
                var propertyName = group.Key;
                var messages = group.Select(e => e.Message);
                var combinedMessage = string.Join("; ", messages);
                
                return Error.Validation(combinedMessage, propertyName);
            })
            .ToList();

        // Add non-validation errors as-is
        var nonValidationErrors = errors
            .Where(e => e.Type != ErrorType.Validation)
            .ToList();
            
        groupedErrors.AddRange(nonValidationErrors);

        return groupedErrors.ToArray();
    }

    /// <summary>
    /// Create validation error with enhanced metadata context for Epic 04.
    /// Includes trace context, tenant information, and validation metadata.
    /// </summary>
    private static Error CreateValidationErrorWithMetadata(
        Error[] groupedErrors, 
        IValidationContext? validationContext)
    {
        var metadata = new Dictionary<string, object>
        {
            ["ValidationErrors"] = groupedErrors.Select(e => new { e.Code, e.Message, e.Type }).ToArray(),
            ["ErrorCount"] = groupedErrors.Length
        };

        // Add trace context if available
        if (validationContext != null)
        {
            metadata["TraceId"] = validationContext.TraceId ?? "unknown";
            metadata["RequestId"] = validationContext.RequestId.ToString();
            
            if (!string.IsNullOrEmpty(validationContext.TenantId))
            {
                metadata["TenantId"] = validationContext.TenantId;
            }
            
            if (!string.IsNullOrEmpty(validationContext.UserId))
            {
                metadata["UserId"] = validationContext.UserId;
            }
            
            // Add feature flags that were checked during validation
            if (validationContext.FeatureFlags.Count > 0)
            {
                metadata["FeatureFlags"] = validationContext.FeatureFlags
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
        }

        return Error.Aggregate(groupedErrors, metadata);
    }

    /// <summary>
    /// Create a failure response with the appropriate type.
    /// Uses reflection to create the correct Result&lt;T&gt; failure response.
    /// </summary>
    private static TResponse CreateFailureResponse<T>(Error error) where T : IResult
    {
        var responseType = typeof(T);
        
        // Handle Result<TValue> types
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = responseType.GetGenericArguments()[0];
            var failureMethod = typeof(Result<>)
                .MakeGenericType(valueType)
                .GetMethod(nameof(Result<object>.Failure), new[] { typeof(Error) });
                
            if (failureMethod != null)
            {
                var result = failureMethod.Invoke(null, new object[] { error });
                return (TResponse)result!;
            }
        }
        
        // Handle basic Result type
        if (responseType == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }
        
        throw new InvalidOperationException(
            $"ValidationBehavior can only be used with Result or Result<T> response types. " +
            $"Got: {responseType.Name}");
    }
}