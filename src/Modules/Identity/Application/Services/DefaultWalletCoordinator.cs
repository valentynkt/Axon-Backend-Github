using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Service implementation for coordinating default wallet setting logic.
/// Consolidates default wallet policy enforcement to eliminate duplication across handlers.
/// </summary>
public sealed class DefaultWalletCoordinator : IDefaultWalletCoordinator
{
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DefaultWalletCoordinator> _logger;

    public DefaultWalletCoordinator(
        TimeProvider timeProvider,
        ILogger<DefaultWalletCoordinator> logger)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<(bool? applied, string? reason)> ApplyIfEligibleAsync(
        AxonPrincipal principal,
        ChainId chainId,
        WalletId walletId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        _logger.LogDebug("Checking default wallet eligibility for principal {PrincipalId}, chain {ChainId}, wallet {WalletId}",
            principal.Id, chainId, walletId);

        // Find the wallet ownership for this principal and wallet
        var ownership = principal.WalletOwnerships
            .FirstOrDefault(w => w.WalletId == walletId && !w.IsDeleted);

        if (ownership is null)
        {
            _logger.LogWarning("Cannot set default wallet {WalletId} - not owned by principal {PrincipalId}",
                walletId, principal.Id);
            return Task.FromResult<(bool?, string?)>((false, IdentityDomainErrors.Wallet.DefaultNotOwnedMessage));
        }

        // Reject unless verified signing
        if (!ownership.IsVerifiedSigning)
        {
            _logger.LogDebug("Cannot set default wallet {WalletId} - not verified signing (AccessMode: {AccessMode}, State: {State})",
                walletId, ownership.AccessMode, ownership.State);
            return Task.FromResult<(bool?, string?)>((false, IdentityDomainErrors.Wallet.WatchOnlyNotAllowedAsDefaultMessage));
        }

        // Check if already default for this chain
        var currentDefaultOwnership = principal.GetDefaultWalletForChain(chainId);
        var currentDefault = currentDefaultOwnership?.WalletId;

        if (currentDefault == walletId)
        {
            _logger.LogDebug("Wallet {WalletId} is already default for chain {ChainId} on principal {PrincipalId}",
                walletId, chainId, principal.Id);
            return Task.FromResult<(bool?, string?)>((null, null)); // Already default, no-op
        }

        // Apply the default setting
        var setDefaultResult = principal.SetDefaultWalletForChain(chainId, walletId, _timeProvider);
        if (setDefaultResult.IsFailure)
        {
            _logger.LogWarning("Failed to set default wallet {WalletId} for chain {ChainId} on principal {PrincipalId}: {Error}",
                walletId, chainId, principal.Id, setDefaultResult.Error.Message);
            return Task.FromResult<(bool?, string?)>((false, setDefaultResult.Error.Message));
        }

        _logger.LogDebug("Successfully set wallet {WalletId} as default for chain {ChainId} on principal {PrincipalId}",
            walletId, chainId, principal.Id);
        return Task.FromResult<(bool?, string?)>((true, null));
    }
}