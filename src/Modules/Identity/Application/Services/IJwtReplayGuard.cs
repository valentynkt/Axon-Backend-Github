using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Service for preventing JWT replay attacks by tracking used JWT IDs (jti claims).
/// Maintains a cache of recently used JWT IDs to prevent the same token from being processed multiple times.
/// </summary>
public interface IJwtReplayGuard
{
    /// <summary>
    /// Checks if a JWT ID has been used recently and marks it as used if not.
    /// This operation is atomic - if the jti is already used, it returns failure.
    /// If the jti is new, it marks it as used and returns success.
    /// </summary>
    /// <param name="jti">The JWT ID from the jti claim</param>
    /// <param name="expiresAt">When this JWT expires (used to determine cache duration)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success if JWT can be processed, Failure if it's a replay attempt</returns>
    Task<Result<Unit, Error>> CheckAndMarkUsedAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
}