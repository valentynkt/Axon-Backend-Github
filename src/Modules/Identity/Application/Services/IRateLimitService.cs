using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Service for rate limiting requests to prevent abuse and maintain service availability.
/// Implements IP-based rate limiting with configurable windows and limits.
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// Checks if a request from the given identifier (e.g., IP address) is allowed.
    /// Returns success if within limits, failure if rate limited.
    /// </summary>
    /// <param name="identifier">The identifier to rate limit (typically IP address)</param>
    /// <param name="operation">The operation being rate limited (e.g., "jwt_exchange")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success if allowed, failure with retry-after information if rate limited</returns>
    Task<Result<Unit, RateLimitError>> CheckRateLimitAsync(string identifier, string operation, CancellationToken cancellationToken = default);
}

/// <summary>
/// Rate limit error with retry-after information
/// </summary>
/// <param name="RetryAfterSeconds">Number of seconds to wait before retrying</param>
/// <param name="RequestsRemaining">Number of requests remaining in current window</param>
/// <param name="WindowResetAt">When the current rate limit window resets</param>
public sealed record RateLimitError(
    int RetryAfterSeconds,
    int RequestsRemaining,
    DateTimeOffset WindowResetAt
)
{
    public string Message => $"Rate limit exceeded. Try again in {RetryAfterSeconds} seconds. Window resets at {WindowResetAt:HH:mm:ss} UTC.";
    
    public Error ToError() => Error.External(Message, "RATE_LIMIT_EXCEEDED");
}