using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Service for handling automatic revocation of pending ownerships
/// when a principal gains exclusive (verified+signing) ownership of a wallet.
/// This logic was moved from the DbContext to follow DDD principles and improve separation of concerns.
/// </summary>
public interface IAutoRevocationService
{
    /// <summary>
    /// Processes auto-revocation for a wallet when a principal gains exclusive ownership.
    /// Revokes all other principals' pending ownerships for the same wallet.
    /// </summary>
    /// <param name="walletId">The wallet ID that now has exclusive ownership</param>
    /// <param name="excludePrincipalId">The principal ID that gained exclusive ownership (will not be revoked)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the number of revoked ownerships</returns>
    Task<Result<int, Error>> ProcessAutoRevocationAsync(
        WalletId walletId,
        AxonUserId excludePrincipalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes auto-revocation based on domain events from a principal.
    /// Checks for verified+signing ownership events that require auto-revocation.
    /// </summary>
    /// <param name="principalId">The principal ID to check for events</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the total number of revoked ownerships</returns>
    Task<Result<int, Error>> ProcessAutoRevocationFromEventsAsync(
        AxonUserId principalId,
        CancellationToken cancellationToken = default);
}