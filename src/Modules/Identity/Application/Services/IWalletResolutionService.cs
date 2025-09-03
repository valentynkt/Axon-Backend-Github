using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Service for resolving existing wallets or registering new ones.
/// Consolidates wallet lookup/creation logic to eliminate duplication across handlers.
/// </summary>
public interface IWalletResolutionService
{
    /// <summary>
    /// Resolves an existing wallet or registers a new one if it doesn't exist.
    /// Handles address canonicalization, uniqueness checking, and proper domain event emission.
    /// </summary>
    /// <param name="chainId">The blockchain chain identifier</param>
    /// <param name="rawAddress">The raw address string to canonicalize</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A tuple containing the wallet and a flag indicating if it was newly created</returns>
    Task<Result<(Wallet wallet, bool wasCreated), Error>> ResolveOrRegisterAsync(
        ChainId chainId,
        string rawAddress,
        CancellationToken cancellationToken = default);
}