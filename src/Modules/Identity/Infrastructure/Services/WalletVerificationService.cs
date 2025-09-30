using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Refactored service implementation for wallet ownership verification using aggregate root pattern.
/// </summary>
public sealed class WalletVerificationService : IWalletVerificationService
{
    private readonly IAxonPrincipalWriteRepository _principalRepository;
    private readonly IWriteUnitOfWork<IdentityModule> _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WalletVerificationService> _logger;
    private const int MaxRetryAttempts = 2;

    public WalletVerificationService(
        IAxonPrincipalWriteRepository principalRepository,
        IWriteUnitOfWork<IdentityModule> unitOfWork,
        TimeProvider timeProvider,
        ILogger<WalletVerificationService> logger)
    {
        _principalRepository = principalRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<WalletOwnership, Error>> VerifyWalletOwnershipAsync(
        WalletId walletId,
        AxonUserId principalId,
        AccessMode accessMode,
        VerificationSource verificationSource,
        CancellationToken cancellationToken = default)
    {
        var retryCount = 0;

        while (retryCount < MaxRetryAttempts)
        {
            try
            {
                // Load the principal aggregate
                var principal = await _principalRepository.GetByIdAsync(principalId, cancellationToken);
                if (principal == null)
                {
                    return Result.Failure<WalletOwnership, Error>(
                        Error.NotFound($"Principal {principalId.Value} not found", "PRINCIPAL.NOT_FOUND"));
                }

                // Check for conflicting ownership by other principals
                var hasConflict = await CheckForConflictingOwnershipAsync(
                    walletId,
                    principalId,
                    cancellationToken);

                if (hasConflict)
                {
                    return Result.Failure<WalletOwnership, Error>(
                        Error.Conflict(
                            "Wallet already has verified signing ownership by another principal",
                            "WALLET.OWNERSHIP.ALREADY_VERIFIED"));
                }

                // Verify ownership through aggregate
                var verifyResult = principal.VerifyWalletOwnership(
                    walletId,
                    accessMode,
                    verificationSource,
                    _timeProvider);

                if (verifyResult.IsFailure)
                {
                    return verifyResult;
                }

                // Update the aggregate
                await _principalRepository.UpdateAsync(principal, cancellationToken);

                // Save changes
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Successfully verified wallet {WalletId} ownership for principal {PrincipalId}",
                    walletId, principalId);

                return verifyResult;
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx &&
                                               pgEx.SqlState == "23505" &&
                                               pgEx.ConstraintName == "ux_exclusive_signing")
            {
                // Race condition: Another transaction created verified signing ownership first
                // This is expected behavior - the unique constraint did its job
                _logger.LogWarning(ex,
                    "Wallet {WalletId} already has verified signing ownership by another principal. Constraint: {Constraint}",
                    walletId, pgEx.ConstraintName);

                return Result.Failure<WalletOwnership, Error>(
                    Error.Conflict(
                        "Wallet already has verified signing ownership by another principal",
                        "WALLET.OWNERSHIP.ALREADY_VERIFIED"));
            }
            catch (DbUpdateConcurrencyException ex)
            {
                retryCount++;
                if (retryCount >= MaxRetryAttempts)
                {
                    _logger.LogError(ex,
                        "Failed to verify wallet ownership after {MaxAttempts} attempts",
                        MaxRetryAttempts);

                    return Result.Failure<WalletOwnership, Error>(
                        Error.Failure("Failed to verify wallet ownership due to concurrent updates",
                            "WALLET.VERIFICATION.CONCURRENCY"));
                }

                _logger.LogWarning(
                    "Concurrency conflict on attempt {Attempt} for wallet {WalletId}. Retrying...",
                    retryCount, walletId);

                // Small delay before retry
                await Task.Delay(50 * retryCount, cancellationToken);
            }
        }

        return Result.Failure<WalletOwnership, Error>(
            Error.Failure("Failed to verify wallet ownership", "WALLET.VERIFICATION.FAILED"));
    }

    public async Task<UnitResult<Error>> RevokeAllPendingOwnershipsAsync(
        WalletId walletId,
        AxonUserId excludePrincipalId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        // Get all principals with pending ownership of this wallet
        var principalsWithPending = await _principalRepository.GetPrincipalsWithPendingOwnershipAsync(
            walletId,
            excludePrincipalId,
            cancellationToken);

        var revokedCount = 0;

        foreach (var principal in principalsWithPending)
        {
            var revokeResult = principal.UpdateWalletOwnershipStatus(
                walletId,
                OwnershipStatus.Revoked,
                _timeProvider,
                reason);

            if (revokeResult.IsSuccess)
            {
                await _principalRepository.UpdateAsync(principal, cancellationToken);
                revokedCount++;
            }
        }

        if (revokedCount > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Revoked {Count} pending ownerships for wallet {WalletId}",
                revokedCount, walletId);
        }

        return UnitResult.Success<Error>();
    }

    public async Task<UnitResult<Error>> SetChainDefaultsAsync(
        AxonUserId principalId,
        IEnumerable<(string chainId, WalletId walletId)> chainDefaults,
        CancellationToken cancellationToken = default)
    {
        var principal = await _principalRepository.GetByIdAsync(principalId, cancellationToken);
        if (principal == null)
        {
            return UnitResult.Failure<Error>(
                Error.NotFound($"Principal {principalId.Value} not found", "PRINCIPAL.NOT_FOUND"));
        }

        var applyResult = principal.ApplyChainDefaultsBatch(chainDefaults, _timeProvider);
        if (applyResult.IsFailure)
        {
            return UnitResult.Failure<Error>(applyResult.Error);
        }

        await _principalRepository.UpdateAsync(principal, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Applied {Count} chain defaults for principal {PrincipalId}",
            applyResult.Value, principalId);

        return UnitResult.Success<Error>();
    }

    public async Task<UnitResult<Error>> RemoveWalletOwnershipAsync(
        AxonUserId principalId,
        WalletId walletId,
        CancellationToken cancellationToken = default)
    {
        var principal = await _principalRepository.GetByIdAsync(principalId, cancellationToken);
        if (principal == null)
        {
            return UnitResult.Failure<Error>(
                Error.NotFound($"Principal {principalId.Value} not found", "PRINCIPAL.NOT_FOUND"));
        }

        var removeResult = principal.RemoveWalletOwnership(walletId, _timeProvider);
        if (removeResult.IsFailure)
        {
            return removeResult;
        }

        await _principalRepository.UpdateAsync(principal, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Removed wallet {WalletId} ownership from principal {PrincipalId}",
            walletId, principalId);

        return UnitResult.Success<Error>();
    }

    private async Task<bool> CheckForConflictingOwnershipAsync(
        WalletId walletId,
        AxonUserId excludePrincipalId,
        CancellationToken cancellationToken)
    {
        // This should be implemented in the repository to check if another principal
        // has verified+signing ownership
        return await _principalRepository.HasVerifiedSigningOwnershipAsync(
            walletId,
            excludePrincipalId,
            cancellationToken);
    }

    public Task<UnitResult<Error>> RevokeOwnershipAsync(
        WalletOwnershipId ownershipId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "RevokeOwnershipAsync called for ownership {OwnershipId} with reason: {Reason}. " +
            "This operation requires finding the ownership through aggregate roots.",
            ownershipId, reason);

        // This is a simplified implementation since we should access through aggregate root
        // In production, we'd need to find which principal owns this ownership
        // Use static method to create failure result
        var error = Error.Failure("NOT_IMPLEMENTED", "Operation not supported in current implementation");
        return Task.FromResult(UnitResult.Failure<Error>(error));
    }

    public async Task<Result<bool, Error>> CanSetAsDefaultAsync(
        AxonUserId principalId,
        WalletId walletId,
        CancellationToken cancellationToken = default)
    {
        var principal = await _principalRepository.GetByIdAsync(principalId, cancellationToken);
        if (principal == null)
        {
            return Result.Failure<bool, Error>(
                Error.NotFound($"Principal not found: {principalId}", "PRINCIPAL.NOT_FOUND"));
        }

        // Check if the principal has verified signing ownership of the wallet
        var hasVerifiedOwnership = principal.HasVerifiedSigningOwnership(walletId);
        return Result.Success<bool, Error>(hasVerifiedOwnership);
    }

    public async Task<Result<IReadOnlyList<WalletOwnership>, Error>> VerifyBatchWalletOwnershipsAsync(
        IEnumerable<(WalletId WalletId, AxonUserId PrincipalId, AccessMode AccessMode, VerificationSource VerificationSource)> requests,
        CancellationToken cancellationToken = default)
    {
        var verifiedOwnerships = new List<WalletOwnership>();

        // Group by principal to minimize database calls
        var groupedRequests = requests.GroupBy(r => r.PrincipalId);

        foreach (var group in groupedRequests)
        {
            var principal = await _principalRepository.GetByIdAsync(group.Key, cancellationToken);
            if (principal == null)
            {
                return Result.Failure<IReadOnlyList<WalletOwnership>, Error>(
                    Error.NotFound($"Principal not found: {group.Key}", "PRINCIPAL.NOT_FOUND"));
            }

            foreach (var request in group)
            {
                var verifyResult = principal.VerifyWalletOwnership(
                    request.WalletId,
                    request.AccessMode,
                    request.VerificationSource,
                    _timeProvider);

                if (verifyResult.IsFailure)
                {
                    return Result.Failure<IReadOnlyList<WalletOwnership>, Error>(verifyResult.Error);
                }

                verifiedOwnerships.Add(verifyResult.Value);
            }

            await _principalRepository.UpdateAsync(principal, cancellationToken);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Batch verified {Count} wallet ownerships",
                verifiedOwnerships.Count);

            return Result.Success<IReadOnlyList<WalletOwnership>, Error>(verifiedOwnerships.AsReadOnly());
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx &&
                                           pgEx.SqlState == "23505" &&
                                           pgEx.ConstraintName == "ux_exclusive_signing")
        {
            // Race condition in batch operation
            _logger.LogWarning(ex,
                "Batch verification failed due to exclusive signing constraint violation. Constraint: {Constraint}",
                pgEx.ConstraintName);

            return Result.Failure<IReadOnlyList<WalletOwnership>, Error>(
                Error.Conflict(
                    "One or more wallets already have verified signing ownership by another principal",
                    "WALLET.OWNERSHIP.BATCH_CONFLICT"));
        }
    }
}