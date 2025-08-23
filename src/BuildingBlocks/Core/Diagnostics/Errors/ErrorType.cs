namespace BuildingBlocks.Core.Diagnostics.Errors;

/// <summary>
/// Error type categorization for routing, handling, and observability.
/// Each type maps to specific HTTP status codes and handling strategies.
/// </summary>
public enum ErrorType
{
    // 4xx Client Errors
    Validation = 1,          // 400 - Bad Request
    NotFound = 2,            // 404 - Not Found
    Conflict = 3,            // 409 - Conflict
    BusinessRule = 4,        // 422 - Unprocessable Entity
    Unauthorized = 5,        // 401 - Unauthorized
    Forbidden = 6,           // 403 - Forbidden
    PreconditionFailed = 7,  // 412 - Precondition Failed (ETag / If-Match failures)
    RateLimit = 8,           // 429 - Too Many Requests
    Cancelled = 9,           // 499 - Client Closed Request (operation cancellation)
    Serialization = 10,      // 400 - Bad Request (serialization/deserialization)
    Concurrency = 11,        // 409 - Conflict (optimistic concurrency)
    
    // 5xx Server Errors
    Internal = 50,           // 500 - Internal Server Error
    Configuration = 51,      // 500 - Internal Server Error (configuration issues)
    External = 52,           // 502 - Bad Gateway (external service errors)
    Network = 53,            // 502 - Bad Gateway (network failures)
    Timeout = 54,            // 504 - Gateway Timeout
    Unavailable = 55,        // 503 - Service Unavailable (dependency unavailable)
    Persistence = 56,        // 507 - Insufficient Storage (storage/persistence layer)
    
    // Composite/Domain Errors
    Aggregate = 90,          // 422 - Unprocessable Entity (aggregate/combined failures)
    Security = 91            // 403 - Forbidden (security violation/suspicious activity)
}