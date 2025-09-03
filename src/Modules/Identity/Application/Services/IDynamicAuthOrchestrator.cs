using Axon.Modules.Identity.Application.DTOs.Exchange;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Orchestrates the Dynamic JWT exchange flow - validation, normalization, and principal/wallet upserts.
/// 
/// This service coordinates the entire exchange process:
/// - Validates JWT tokens using JWKS from Dynamic.xyz
/// - Creates or updates Axon principals and credentials
/// - Processes associated wallets with activity tracking and ownership linking
/// - Applies the 20/80 rule for default wallet assignment per chain
/// - Handles errors gracefully with detailed result metrics
/// 
/// The exchange is idempotent and can be safely retried.
/// </summary>
public interface IDynamicAuthOrchestrator
{
    /// <summary>
    /// Exchanges a Dynamic JWT for an Axon identity, processing all wallets and setting up defaults.
    /// 
    /// This method performs the complete exchange flow:
    /// 1. Validates the JWT signature and claims using Dynamic's JWKS endpoint
    /// 2. Creates or updates the Axon principal based on the credential information  
    /// 3. Processes each wallet: updates activity, links ownership, applies defaults per chain
    /// 4. Updates the user profile if needed (currently no-op for Dynamic)
    /// 5. Returns detailed metrics for telemetry and logging
    /// 
    /// The operation is designed to be fault-tolerant - individual wallet failures don't stop the exchange.
    /// </summary>
    /// <param name="jwt">The Dynamic JWT token from the Authorization header</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>Exchange outcome with detailed metrics including creation status, wallet counts, conflicts, etc.</returns>
    Task<Result<ExchangeOutcome, Error>> ExchangeAsync(string jwt, CancellationToken cancellationToken = default);
}

/// <summary>
/// Bridge interface for validating and normalizing Dynamic JWT tokens within Application layer
/// </summary>
public interface IDynamicJwtBridge
{
    /// <summary>
    /// Validates JWT token and returns simplified user data for Application layer
    /// </summary>
    Task<Result<ExchangeUserData, Error>> ValidateAndNormalizeAsync(string jwt, CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of Dynamic JWT exchange operation with summary metrics
/// </summary>
public sealed record ExchangeOutcome(
    string AxonId,
    bool Created,
    int WalletsProcessed,
    int WalletsLinked,
    int DefaultsApplied,
    int Skipped,
    int Conflicts
);