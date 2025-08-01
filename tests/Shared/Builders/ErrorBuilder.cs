using Axon.Shared.Common;

namespace Axon.Tests.Shared.Builders;

/// <summary>
/// Builder pattern for creating Error instances in tests with fluent API
/// </summary>
public sealed class ErrorBuilder
{
    private string _message = "Default error message";
    private string _code = "DEFAULT_ERROR";
    private ErrorType _type = ErrorType.InternalError;
    private Exception? _innerException = null;

    private ErrorBuilder() { }

    /// <summary>
    /// Creates a new ErrorBuilder
    /// </summary>
    public static ErrorBuilder New() => new();

    /// <summary>
    /// Creates a validation error builder
    /// </summary>
    public static ErrorBuilder Validation() => new() { _type = ErrorType.Validation, _code = "VALIDATION_ERROR" };

    /// <summary>
    /// Creates a not found error builder
    /// </summary>
    public static ErrorBuilder NotFound() => new() { _type = ErrorType.NotFound, _code = "NOT_FOUND" };

    /// <summary>
    /// Creates a conflict error builder
    /// </summary>
    public static ErrorBuilder Conflict() => new() { _type = ErrorType.Conflict, _code = "CONFLICT" };

    /// <summary>
    /// Creates an internal error builder
    /// </summary>
    public static ErrorBuilder InternalError() => new() { _type = ErrorType.InternalError, _code = "INTERNAL_ERROR" };

    /// <summary>
    /// Creates an external service error builder
    /// </summary>
    public static ErrorBuilder ExternalService() => new() { _type = ErrorType.ExternalService, _code = "EXTERNAL_SERVICE_ERROR" };

    /// <summary>
    /// Creates an unauthorized error builder
    /// </summary>
    public static ErrorBuilder Unauthorized() => new() { _type = ErrorType.Unauthorized, _code = "UNAUTHORIZED" };

    /// <summary>
    /// Creates a forbidden error builder
    /// </summary>
    public static ErrorBuilder Forbidden() => new() { _type = ErrorType.Forbidden, _code = "FORBIDDEN" };

    /// <summary>
    /// Sets the error message
    /// </summary>
    public ErrorBuilder WithMessage(string message)
    {
        _message = message;
        return this;
    }

    /// <summary>
    /// Sets the error code
    /// </summary>
    public ErrorBuilder WithCode(string code)
    {
        _code = code;
        return this;
    }

    /// <summary>
    /// Sets the error type
    /// </summary>
    public ErrorBuilder WithType(ErrorType type)
    {
        _type = type;
        return this;
    }

    /// <summary>
    /// Sets the inner exception
    /// </summary>
    public ErrorBuilder WithInnerException(Exception innerException)
    {
        _innerException = innerException;
        return this;
    }

    /// <summary>
    /// Builds the Error instance using the appropriate factory method
    /// </summary>
    public Error Build()
    {
        return _type switch
        {
            ErrorType.Validation => Error.Validation(_message, _code),
            ErrorType.NotFound => Error.NotFound(_message, _code),
            ErrorType.Conflict => Error.Conflict(_message, _code),
            ErrorType.InternalError => Error.InternalError(_message, _code, _innerException),
            ErrorType.ExternalService => Error.ExternalService(_message, _code, _innerException),
            ErrorType.Unauthorized => Error.Unauthorized(_message, _code),
            ErrorType.Forbidden => Error.Forbidden(_message, _code),
            _ => throw new ArgumentOutOfRangeException($"Unsupported error type: {_type}")
        };
    }

    /// <summary>
    /// Implicit conversion to Error
    /// </summary>
    public static implicit operator Error(ErrorBuilder builder) => builder.Build();
}

/// <summary>
/// Static factory methods for ErrorBuilder with common patterns
/// </summary>
public static class ErrorBuilders
{
    /// <summary>
    /// Creates a validation error for required fields
    /// </summary>
    public static ErrorBuilder RequiredField(string fieldName) =>
        ErrorBuilder.Validation()
            .WithMessage($"{fieldName} is required")
            .WithCode("FIELD_REQUIRED");

    /// <summary>
    /// Creates a validation error for invalid format
    /// </summary>
    public static ErrorBuilder InvalidFormat(string fieldName, string expectedFormat) =>
        ErrorBuilder.Validation()
            .WithMessage($"{fieldName} must be in format: {expectedFormat}")
            .WithCode("INVALID_FORMAT");

    /// <summary>
    /// Creates a validation error for out of range values
    /// </summary>
    public static ErrorBuilder OutOfRange(string fieldName, string range) =>
        ErrorBuilder.Validation()
            .WithMessage($"{fieldName} must be within range: {range}")
            .WithCode("OUT_OF_RANGE");

    /// <summary>
    /// Creates a not found error for entities
    /// </summary>
    public static ErrorBuilder EntityNotFound(string entityType, string identifier) =>
        ErrorBuilder.NotFound()
            .WithMessage($"{entityType} with identifier '{identifier}' was not found")
            .WithCode($"{entityType.ToUpperInvariant()}_NOT_FOUND");

    /// <summary>
    /// Creates a conflict error for duplicate entities
    /// </summary>
    public static ErrorBuilder DuplicateEntity(string entityType, string identifier) =>
        ErrorBuilder.Conflict()
            .WithMessage($"{entityType} with identifier '{identifier}' already exists")
            .WithCode($"{entityType.ToUpperInvariant()}_ALREADY_EXISTS");

    /// <summary>
    /// Creates a database connection error
    /// </summary>
    public static ErrorBuilder DatabaseConnection(Exception? innerException = null) =>
        ErrorBuilder.InternalError()
            .WithMessage("Database connection failed")
            .WithCode("DB_CONNECTION_ERROR")
            .WithInnerException(innerException ?? new InvalidOperationException("Connection timeout"));

    /// <summary>
    /// Creates an external API error
    /// </summary>
    public static ErrorBuilder ExternalApi(string apiName, int? statusCode = null, Exception? innerException = null) =>
        ErrorBuilder.ExternalService()
            .WithMessage($"External API '{apiName}' returned an error" + (statusCode.HasValue ? $" (Status: {statusCode})" : ""))
            .WithCode($"{apiName.ToUpperInvariant()}_API_ERROR")
            .WithInnerException(innerException);

    /// <summary>
    /// Creates an authentication required error
    /// </summary>
    public static ErrorBuilder AuthenticationRequired() =>
        ErrorBuilder.Unauthorized()
            .WithMessage("Authentication is required to access this resource")
            .WithCode("AUTHENTICATION_REQUIRED");

    /// <summary>
    /// Creates an invalid credentials error
    /// </summary>
    public static ErrorBuilder InvalidCredentials() =>
        ErrorBuilder.Unauthorized()
            .WithMessage("The provided credentials are invalid")
            .WithCode("INVALID_CREDENTIALS");

    /// <summary>
    /// Creates an insufficient permissions error
    /// </summary>
    public static ErrorBuilder InsufficientPermissions(string resource) =>
        ErrorBuilder.Forbidden()
            .WithMessage($"Insufficient permissions to access '{resource}'")
            .WithCode("INSUFFICIENT_PERMISSIONS");

    /// <summary>
    /// Creates a rate limit exceeded error
    /// </summary>
    public static ErrorBuilder RateLimitExceeded(TimeSpan retryAfter) =>
        ErrorBuilder.ExternalService()
            .WithMessage($"Rate limit exceeded. Retry after {retryAfter.TotalSeconds} seconds")
            .WithCode("RATE_LIMIT_EXCEEDED");

    /// <summary>
    /// Creates a timeout error
    /// </summary>
    public static ErrorBuilder Timeout(string operation, TimeSpan timeout) =>
        ErrorBuilder.ExternalService()
            .WithMessage($"Operation '{operation}' timed out after {timeout.TotalSeconds} seconds")
            .WithCode("OPERATION_TIMEOUT")
            .WithInnerException(new TimeoutException($"Operation timed out after {timeout}"));

    /// <summary>
    /// Creates a configuration error
    /// </summary>
    public static ErrorBuilder Configuration(string configKey, string issue) =>
        ErrorBuilder.InternalError()
            .WithMessage($"Configuration error for '{configKey}': {issue}")
            .WithCode("CONFIGURATION_ERROR");
}