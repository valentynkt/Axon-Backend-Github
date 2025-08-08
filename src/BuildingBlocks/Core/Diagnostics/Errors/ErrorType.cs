namespace BuildingBlocks.Core.Diagnostics.Errors;

/// <summary>
/// Error type categorization for routing, handling, and observability.
/// Each type maps to specific HTTP status codes and handling strategies.
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// Input validation failures (400 Bad Request)
    /// Use for: Invalid input format, missing required fields, constraint violations
    /// </summary>
    Validation = 1,
    
    /// <summary>
    /// Resource not found (404 Not Found)
    /// Use for: Entity not found by ID, missing resources, invalid routes
    /// </summary>
    NotFound = 2,
    
    /// <summary>
    /// Resource conflict, typically optimistic concurrency violations (409 Conflict)
    /// Use for: Duplicate keys, version conflicts, resource already exists
    /// </summary>
    Conflict = 3,
    
    /// <summary>
    /// Business rule violations (422 Unprocessable Entity)
    /// Use for: Domain rule violations, workflow violations, business logic errors
    /// </summary>
    BusinessRule = 4,
    
    /// <summary>
    /// Authentication required (401 Unauthorized)
    /// Use for: Missing authentication, invalid credentials, expired tokens
    /// </summary>
    Unauthorized = 5,
    
    /// <summary>
    /// Permission denied (403 Forbidden)
    /// Use for: Insufficient permissions, access denied, role violations
    /// </summary>
    Forbidden = 6,
    
    /// <summary>
    /// Internal server errors (500 Internal Server Error)
    /// Use for: Unexpected exceptions, system failures, programming errors
    /// </summary>
    Internal = 7,
    
    /// <summary>
    /// External service failures (502 Bad Gateway)
    /// Use for: Third-party service failures, API integration errors
    /// </summary>
    External = 8,
    
    /// <summary>
    /// Operation timeout (504 Gateway Timeout)
    /// Use for: Request timeouts, long-running operations, deadline exceeded
    /// </summary>
    Timeout = 9,
    
    /// <summary>
    /// Operation cancelled by user or system (499 Client Closed Request)
    /// Use for: User cancellation, system shutdown, token cancellation
    /// </summary>
    Cancelled = 10,
    
    /// <summary>
    /// Rate limiting exceeded (429 Too Many Requests)
    /// Use for: API rate limits, throttling, quota exceeded
    /// </summary>
    RateLimit = 11,
    
    /// <summary>
    /// Persistence layer errors (507 Insufficient Storage)
    /// Use for: Database errors, storage failures, transaction failures
    /// </summary>
    Persistence = 12,
    
    /// <summary>
    /// Aggregate/domain model errors (422 Unprocessable Entity)
    /// Use for: Aggregate root violations, invariant failures, domain state errors
    /// </summary>
    Aggregate = 13,
    
    /// <summary>
    /// Configuration errors (500 Internal Server Error)
    /// Use for: Missing config values, invalid configuration, setup errors
    /// </summary>
    Configuration = 14,
    
    /// <summary>
    /// Network-related errors (502 Bad Gateway)
    /// Use for: Connection failures, DNS resolution, network timeouts
    /// </summary>
    Network = 15,
    
    /// <summary>
    /// Security-related errors (403 Forbidden)
    /// Use for: Security violations, encryption failures, suspicious activity
    /// </summary>
    Security = 16
}