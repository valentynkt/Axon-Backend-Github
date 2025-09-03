using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Specifications.AxonPrincipals;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Service implementation for enforcing wallet ownership and credential invariants.
/// Uses repository specifications to maintain clean separation of concerns.
/// </summary>
public sealed class WalletOwnershipService : IWalletOwnershipService
{
    private readonly IAxonPrincipalReadRepository _principalReadRepository;
    private readonly ILogger<WalletOwnershipService> _logger;

    public WalletOwnershipService(
        IAxonPrincipalReadRepository principalReadRepository,
        ILogger<WalletOwnershipService> logger)
    {
        _principalReadRepository = principalReadRepository ?? throw new ArgumentNullException(nameof(principalReadRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<AxonPrincipal?, Error>> CheckCredentialUniquenessAsync(
        ProviderType providerType, 
        string issuer, 
        string subject,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Checking credential uniqueness for {ProviderType}:{Issuer}:{Subject}", 
                providerType.Value, issuer, subject);

            var spec = new PrincipalByCredentialSpec(providerType, issuer, subject);
            var existingPrincipal = await _principalReadRepository.FirstOrDefaultAsync(spec, cancellationToken);

            if (existingPrincipal is not null)
            {
                _logger.LogDebug("Found existing principal {PrincipalId} with credential {ProviderType}:{Issuer}:{Subject}",
                    existingPrincipal.Id, providerType.Value, issuer, subject);
            }

            return Result.Success<AxonPrincipal?, Error>(existingPrincipal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking credential uniqueness for {ProviderType}:{Issuer}:{Subject}", 
                providerType.Value, issuer, subject);
            return Result.Failure<AxonPrincipal?, Error>(
                Error.Failure("Failed to check credential uniqueness", "IDENTITY.CREDENTIAL.CHECK_FAILED"));
        }
    }

    public async Task<Result<OwnershipConflictResult, Error>> CheckWalletOwnershipConflictAsync(
        WalletId walletId,
        AxonId requestingPrincipalId,
        ProofType proofType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Checking wallet ownership conflict for wallet {WalletId} by principal {PrincipalId} with proof {ProofType}",
                walletId, requestingPrincipalId, proofType.Value);

            // Only check conflicts for verified signing proof types
            if (!proofType.IsVerifiedSigning)
            {
                _logger.LogDebug("Non-verified signing proof type {ProofType}, no conflict possible", proofType.Value);
                return Result.Success<OwnershipConflictResult, Error>(new NoConflict());
            }

            // Check if wallet already has verified signing owner
            var verifiedSigningOwnerSpec = new WalletOwnedByVerifiedSigningSpec(walletId);
            var existingOwnerPrincipal = await _principalReadRepository.FirstOrDefaultAsync(verifiedSigningOwnerSpec, cancellationToken);

            if (existingOwnerPrincipal is not null && existingOwnerPrincipal.Id != requestingPrincipalId)
            {
                _logger.LogWarning("Wallet {WalletId} already has verified signing owner {ExistingOwner}, cannot link to {RequestingPrincipal}",
                    walletId, existingOwnerPrincipal.Id, requestingPrincipalId);
                
                return Result.Success<OwnershipConflictResult, Error>(new Conflict(existingOwnerPrincipal.Id));
            }

            _logger.LogDebug("No wallet ownership conflict found for wallet {WalletId}", walletId);
            return Result.Success<OwnershipConflictResult, Error>(new NoConflict());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking wallet ownership conflict for wallet {WalletId} by principal {PrincipalId}",
                walletId, requestingPrincipalId);
            return Result.Failure<OwnershipConflictResult, Error>(
                Error.Failure("Failed to check wallet ownership conflict", "IDENTITY.WALLET.CONFLICT_CHECK_FAILED"));
        }
    }

    public async Task<Result<bool, Error>> ValidateWalletLinkingAsync(
        AxonPrincipal principal,
        WalletId walletId,
        ProofType proofType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        
        try
        {
            _logger.LogDebug("Validating wallet linking for principal {PrincipalId} to wallet {WalletId} with proof {ProofType}",
                principal.Id, walletId, proofType.Value);

            // Check wallet ownership conflicts
            var conflictResult = await CheckWalletOwnershipConflictAsync(
                walletId, principal.Id, proofType, cancellationToken);

            if (conflictResult.IsFailure)
            {
                return Result.Failure<bool, Error>(conflictResult.Error);
            }

            // Handle conflict result
            if (conflictResult.Value is Conflict conflict)
            {
                return Result.Failure<bool, Error>(
                    Error.Conflict($"Wallet is already owned by another principal with verified signing: {conflict.ExistingOwnerPrincipalId}",
                        "IDENTITY.WALLET.VERIFIED_SIGNING_CONFLICT"));
            }

            // Additional business rule validations could be added here
            // For example: checking max wallets per principal, wallet activity requirements, etc.

            _logger.LogDebug("Wallet linking validation passed for principal {PrincipalId} to wallet {WalletId}",
                principal.Id, walletId);

            return Result.Success<bool, Error>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating wallet linking for principal {PrincipalId} to wallet {WalletId}",
                principal.Id, walletId);
            return Result.Failure<bool, Error>(
                Error.Failure("Failed to validate wallet linking", "IDENTITY.WALLET.LINKING_VALIDATION_FAILED"));
        }
    }
}