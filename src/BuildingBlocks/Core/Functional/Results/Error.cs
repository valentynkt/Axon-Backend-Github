using Ardalis.GuardClauses;

namespace BuildingBlocks.Core.Results;

/// <summary>
/// Represents an error that can occur during operation execution.
/// Provides a comprehensive error model with categorization, context, and metadata support.
/// </summary>
public sealed record Error
{
    /// <summary>
    /// Unique error code for programmatic identification
    /// </summary>
    public string Code { get; }
    
    /// <summary>
    /// Human-readable error message
    /// </summary>
    public string Message { get; }
    
    /// <summary>
    /// Category/type of the error for proper handling
    /// </summary>
    public ErrorType Type { get; }
    
    /// <summary>
    /// Optional inner exception that caused this error
    /// </summary>
    public System.Exception? InnerException { get; }
    
    /// <summary>
    /// Optional metadata for additional error context
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; }

    private Error(string code, string message, ErrorType type, System.Exception? innerException = null, IReadOnlyDictionary<string, object>? metadata = null)
    {
        Code = Guard.Against.NullOrWhiteSpace(code, nameof(code));
        Message = Guard.Against.NullOrWhiteSpace(message, nameof(message));
        Type = type;
        InnerException = innerException;
        Metadata = metadata;
    }

    /// <summary>
    /// Creates a validation error
    /// </summary>
    public static Error Validation(string message, string code = "VALIDATION_ERROR", IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Validation, metadata: metadata);

    /// <summary>
    /// Creates a not found error
    /// </summary>
    public static Error NotFound(string message, string code = "NOT_FOUND", IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.NotFound, metadata: metadata);

    /// <summary>
    /// Creates a conflict error
    /// </summary>
    public static Error Conflict(string message, string code = "CONFLICT", IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Conflict, metadata: metadata);

    /// <summary>
    /// Creates an internal/system error
    /// </summary>
    public static Error InternalError(string message, string code = "INTERNAL_ERROR", System.Exception? innerException = null, IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.InternalError, innerException, metadata);

    /// <summary>
    /// Creates an external service error
    /// </summary>
    public static Error ExternalService(string message, string code = "EXTERNAL_SERVICE_ERROR", System.Exception? innerException = null, IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.ExternalService, innerException, metadata);

    /// <summary>
    /// Creates an unauthorized error
    /// </summary>
    public static Error Unauthorized(string message, string code = "UNAUTHORIZED", IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Unauthorized, metadata: metadata);

    /// <summary>
    /// Creates a forbidden error
    /// </summary>
    public static Error Forbidden(string message, string code = "FORBIDDEN", IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Forbidden, metadata: metadata);

    /// <summary>
    /// Creates a persistence/database error
    /// </summary>
    public static Error Persistence(string message, string code = "PERSISTENCE_ERROR", System.Exception? innerException = null, IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Persistence, innerException, metadata);

    /// <summary>
    /// Creates a business rule violation error
    /// </summary>  
    public static Error BusinessRule(string message, string code = "BUSINESS_RULE_VIOLATION", IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.BusinessRule, metadata: metadata);

    /// <summary>
    /// Creates a concurrency error
    /// </summary>
    public static Error Concurrency(string message, string code = "CONCURRENCY_ERROR", IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Concurrency, metadata: metadata);

    /// <summary>
    /// Creates a timeout error
    /// </summary>
    public static Error Timeout(string message, string code = "TIMEOUT_ERROR", System.Exception? innerException = null, IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, ErrorType.Timeout, innerException, metadata);

    /// <summary>
    /// Creates a custom error with specified type
    /// </summary>
    public static Error Custom(string code, string message, ErrorType type, System.Exception? innerException = null, IReadOnlyDictionary<string, object>? metadata = null) =>
        new(code, message, type, innerException, metadata);

    /// <summary>
    /// Creates an error from an exception
    /// </summary>
    public static Error FromException(System.Exception exception, string? code = null, ErrorType type = ErrorType.InternalError)
    {
        var errorCode = code ?? exception.GetType().Name.Replace("Exception", "").ToUpperInvariant();
        return new(errorCode, exception.Message, type, exception);
    }

    /// <summary>
    /// Adds metadata to the error
    /// </summary>
    public Error WithMetadata(string key, object value)
    {
        var existingMetadata = Metadata ?? new Dictionary<string, object>();
        var newMetadata = new Dictionary<string, object>(existingMetadata) { [key] = value };
        return new Error(Code, Message, Type, InnerException, newMetadata);
    }

    /// <summary>
    /// Adds multiple metadata entries to the error
    /// </summary>
    public Error WithMetadata(IReadOnlyDictionary<string, object> metadata)
    {
        if (metadata.Count == 0) return this;
        
        var existingMetadata = Metadata ?? new Dictionary<string, object>();
        var newMetadata = new Dictionary<string, object>(existingMetadata);
        foreach (var kvp in metadata)
        {
            newMetadata[kvp.Key] = kvp.Value;
        }
        return new Error(Code, Message, Type, InnerException, newMetadata);
    }

    /// <summary>
    /// Gets metadata value by key
    /// </summary>
    public T? GetMetadata<T>(string key) where T : class
    {
        return Metadata?.TryGetValue(key, out var value) == true ? value as T : null;
    }

    /// <summary>
    /// Checks if metadata contains a specific key
    /// </summary>
    public bool HasMetadata(string key) => Metadata?.ContainsKey(key) == true;

    public override string ToString() => $"[{Type}] {Code}: {Message}";
}

/// <summary>
/// Represents the type/category of error that occurred.
/// Used for proper error handling and response mapping.
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// Input validation failed
    /// </summary>
    Validation,
    
    /// <summary>
    /// Requested resource was not found
    /// </summary>
    NotFound,
    
    /// <summary>
    /// Operation conflicts with current state
    /// </summary>
    Conflict,
    
    /// <summary>
    /// Internal system error
    /// </summary>
    InternalError,
    
    /// <summary>
    /// External service/dependency error
    /// </summary>
    ExternalService,
    
    /// <summary>
    /// Authentication required
    /// </summary>
    Unauthorized,
    
    /// <summary>
    /// Access denied
    /// </summary>
    Forbidden,
    
    /// <summary>
    /// Database/persistence error
    /// </summary>
    Persistence,
    
    /// <summary>
    /// Business rule violation
    /// </summary>
    BusinessRule,
    
    /// <summary>
    /// Concurrency/optimistic locking error
    /// </summary>
    Concurrency,
    
    /// <summary>
    /// Operation timeout
    /// </summary>
    Timeout
}