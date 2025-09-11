using System.Text.Json.Serialization;

namespace Axon.Api.Contracts.Common;

/// <summary>
/// Standardized API error response envelope for consistent client integration.
/// Maps domain errors to HTTP status codes with privacy-safe details.
/// </summary>
public record ApiError
{
    /// <summary>
    /// Machine-readable error code for client handling.
    /// Examples: "VALIDATION_ERROR", "WALLET_OWNERSHIP_CONFLICT", "RATE_LIMIT_EXCEEDED"
    /// </summary>
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    /// <summary>
    /// Human-readable error message for display purposes.
    /// Should not expose sensitive system details.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// Optional structured details providing context-specific information.
    /// For 409 conflicts, contains only chainId and address (privacy-safe).
    /// </summary>
    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Details { get; init; }

    /// <summary>
    /// Creates an ApiError with the specified parameters.
    /// </summary>
    /// <param name="code">Machine-readable error code</param>
    /// <param name="message">Human-readable error message</param>
    /// <param name="details">Optional structured details</param>
    /// <returns>New ApiError instance</returns>
    public static ApiError Create(string code, string message, object? details = null) =>
        new() { Code = code, Message = message, Details = details };

    /// <summary>
    /// Creates a validation error for malformed requests or invalid parameters.
    /// </summary>
    /// <param name="message">Validation error message</param>
    /// <param name="details">Optional validation details</param>
    /// <returns>Validation ApiError</returns>
    public static ApiError Validation(string message, object? details = null) =>
        Create("VALIDATION_ERROR", message, details);

    /// <summary>
    /// Creates an authentication error for missing or invalid JWT tokens.
    /// </summary>
    /// <param name="message">Authentication error message</param>
    /// <returns>Authentication ApiError</returns>
    public static ApiError Unauthorized(string message = "Authentication required") =>
        Create("UNAUTHORIZED", message);

    /// <summary>
    /// Creates a wallet ownership conflict error with privacy-safe details.
    /// Only includes chainId and address, never Principal IDs.
    /// </summary>
    /// <param name="chainId">Blockchain identifier</param>
    /// <param name="address">Wallet address</param>
    /// <returns>Wallet ownership conflict ApiError</returns>
    public static ApiError WalletOwnershipConflict(string chainId, string address) =>
        Create("WALLET_OWNERSHIP_CONFLICT", "wallet already owned", 
            new { chainId, address });

    /// <summary>
    /// Creates a business rule violation error for domain constraint failures.
    /// </summary>
    /// <param name="message">Business rule violation message</param>
    /// <param name="details">Optional business rule details</param>
    /// <returns>Business rule violation ApiError</returns>
    public static ApiError BusinessRuleViolation(string message, object? details = null) =>
        Create("BUSINESS_RULE_VIOLATION", message, details);

    /// <summary>
    /// Creates a rate limit exceeded error with retry information.
    /// </summary>
    /// <param name="message">Rate limit error message</param>
    /// <param name="retryAfter">Seconds until rate limit resets</param>
    /// <returns>Rate limit ApiError</returns>
    public static ApiError RateLimitExceeded(string message = "Rate limit exceeded", int? retryAfter = null) =>
        Create("RATE_LIMIT_EXCEEDED", message, 
            retryAfter.HasValue ? new { retryAfter = retryAfter.Value } : null);

    /// <summary>
    /// Creates an internal server error for unexpected system failures.
    /// </summary>
    /// <param name="message">Internal error message</param>
    /// <returns>Internal server error ApiError</returns>
    public static ApiError InternalError(string message = "An internal error occurred") =>
        Create("INTERNAL_ERROR", message);
}