namespace BuildingBlocks.Core.Diagnostics.Errors;

/// <summary>
/// Error type categorization for routing, handling, and observability.
/// Each type maps to specific HTTP status codes and handling strategies.
/// </summary>
public enum ErrorType
{
    // 4xx
    Validation = 1,          // 400
    NotFound = 2,            // 404
    Conflict = 3,            // 409
    BusinessRule = 4,        // 422
    Unauthorized = 5,        // 401
    Forbidden = 6,           // 403
    PreconditionFailed = 7,  // 412 (ETag / If-Match failures)
    RateLimit = 8,           // 429
    Cancelled = 9,           // 499 (non-standard but widely used)
    Serialization = 10,      // 400 (serialization/deserialization)
    Concurrency = 11,        // 409/412 (optimistic concurrency)
    
    // 5xx
    Internal = 50,           // 500
    Configuration = 51,      // 500
    External = 52,           // 502
    Network = 53,            // 502
    Timeout = 54,            // 504
    Unavailable = 55,        // 503 (dependency unavailable)
    Persistence = 56,        // 507 (storage/persistence layer)
    
    // domain / composed
    Aggregate = 90,          // 422 aggregate/combined failures
    Security = 91            // 403 security violation/suspicious activity
}