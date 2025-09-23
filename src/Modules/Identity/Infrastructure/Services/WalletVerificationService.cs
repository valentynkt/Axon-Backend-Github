using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Service implementation for wallet ownership verification with transaction guards and row-level locking.
/// </summary>
public sealed class WalletVerificationService : IWalletVerificationService
{
    private readonly IdentityWriteDbContext _dbContext;
    private readonly ILogger<WalletVerificationService> _logger;
    private const int MaxRetryAttempts = 2;

    public WalletVerificationService(
        IdentityWriteDbContext dbContext,
        ILogger<WalletVerificationService> logger)
    {
        _dbContext = dbContext;
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
                return await ExecuteVerificationTransactionAsync(
                    walletId,
                    principalId,
                    accessMode,
                    verificationSource,
                    cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                retryCount++;
                _logger.LogWarning(
                    "Unique constraint violation during wallet verification. Attempt {RetryCount}/{MaxRetryAttempts}. " +
                    "WalletId: {WalletId}, PrincipalId: {PrincipalId}",
                    retryCount, MaxRetryAttempts, walletId, principalId);

                if (retryCount >= MaxRetryAttempts)
                {
                    return Result.Failure<WalletOwnership, Error>(
                        Error.Conflict("Failed to verify ownership after retries due to concurrent modifications",
                            "WALLET.VERIFICATION.RACE_CONDITION"));
                }

                // Small delay before retry
                await Task.Delay(100 * retryCount, cancellationToken);
            }
        }

        return Result.Failure<WalletOwnership, Error>(
            Error.Failure("Unexpected error during verification", "WALLET.VERIFICATION.UNEXPECTED"));
    }

    private async Task<Result<WalletOwnership, Error>> ExecuteVerificationTransactionAsync(
        WalletId walletId,
        AxonUserId principalId,
        AccessMode accessMode,
        VerificationSource verificationSource,
        CancellationToken cancellationToken)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            // Step 1: Lock the wallet row first using raw SQL for explicit FOR UPDATE
            var wallet = await LockWalletAsync(walletId, cancellationToken);
            if (wallet == null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<WalletOwnership, Error>(
                    Error.NotFound($"Wallet {walletId.Value} not found", "WALLET.NOT_FOUND"));
            }

            // Step 2: Lock competing ownerships with consistent ordering (by wallet_id ASC)
            var existingOwnerships = await LockCompetingOwnershipsAsync(walletId, cancellationToken);

            // Step 3: Check for existing verified+signing ownership
            var verifiedSigningOwnership = existingOwnerships
                .FirstOrDefault(o => o.Status == OwnershipStatus.Verified &&
                                   o.AccessMode == AccessMode.Signing &&
                                   !o.IsDeleted);

            // If another principal already has verified+signing, return 409 Conflict
            if (verifiedSigningOwnership != null && verifiedSigningOwnership.PrincipalId != principalId)
            {
                await transaction.RollbackAsync(cancellationToken);

                _logger.LogWarning(
                    "Wallet {WalletId} already has verified signing ownership by principal {ExistingPrincipalId}. " +
                    "Requested by principal {RequestedPrincipalId}",
                    walletId, verifiedSigningOwnership.PrincipalId, principalId);

                return Result.Failure<WalletOwnership, Error>(
                    Error.Conflict(
                        $"Wallet already has verified signing ownership by another principal",
                        "WALLET.OWNERSHIP.ALREADY_VERIFIED"));
            }

            // Step 4: Find or create ownership for the requesting principal
            var candidateOwnership = existingOwnerships
                .FirstOrDefault(o => o.PrincipalId == principalId && !o.IsDeleted);

            if (candidateOwnership == null)
            {
                candidateOwnership = WalletOwnership.Create(
                    principalId,
                    walletId,
                    accessMode,
                    OwnershipStatus.Verified,
                    verificationSource);

                await _dbContext.WalletOwnerships.AddAsync(candidateOwnership, cancellationToken);

                _logger.LogInformation(
                    "Created new ownership for principal {PrincipalId} on wallet {WalletId} with status {Status}",
                    principalId, walletId, OwnershipStatus.Verified);
            }
            else
            {
                // Update existing ownership to verified
                var updateResult = candidateOwnership.UpdateStatus(OwnershipStatus.Verified);
                if (updateResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure<WalletOwnership, Error>(updateResult.Error);
                }

                // Update access mode if different
                if (candidateOwnership.AccessMode != accessMode)
                {
                    var modeResult = candidateOwnership.UpdateAccessMode(accessMode);
                    if (modeResult.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result.Failure<WalletOwnership, Error>(modeResult.Error);
                    }
                }

                _logger.LogInformation(
                    "Updated ownership for principal {PrincipalId} on wallet {WalletId} to status {Status}",
                    principalId, walletId, OwnershipStatus.Verified);
            }

            // Step 5: Auto-revoke other pending/unverified ownerships on the same wallet (silent operation)
            if (accessMode == AccessMode.Signing)
            {
                foreach (var ownership in existingOwnerships)
                {
                    if (ownership.Id != candidateOwnership.Id &&
                        ownership.Status != OwnershipStatus.Verified &&
                        !ownership.IsDeleted)
                    {
                        var revokeResult = ownership.UpdateStatus(
                            OwnershipStatus.Revoked,
                            "Auto-revoked due to new verified signing ownership");

                        if (revokeResult.IsSuccess)
                        {
                            _logger.LogInformation(
                                "Auto-revoked pending ownership {OwnershipId} for principal {PrincipalId} on wallet {WalletId}",
                                ownership.Id, ownership.PrincipalId, walletId);
                        }
                    }
                }
            }

            // Save changes and commit transaction
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully verified ownership for principal {PrincipalId} on wallet {WalletId} with access mode {AccessMode}",
                principalId, walletId, accessMode);

            return Result.Success<WalletOwnership, Error>(candidateOwnership);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogError(ex,
                "Error during wallet ownership verification for principal {PrincipalId} on wallet {WalletId}",
                principalId, walletId);

            throw;
        }
    }

    private async Task<Wallet?> LockWalletAsync(WalletId walletId, CancellationToken cancellationToken)
    {
        // Use raw SQL for explicit row-level lock with FOR UPDATE
        var wallet = await _dbContext.Wallets
            .FromSqlRaw(
                "SELECT * FROM identity.wallet WHERE id = {0} FOR UPDATE",
                walletId.Value.ToString())
            .FirstOrDefaultAsync(cancellationToken);

        return wallet;
    }

    private async Task<List<WalletOwnership>> LockCompetingOwnershipsAsync(
        WalletId walletId,
        CancellationToken cancellationToken)
    {
        // Use raw SQL for explicit row-level lock with FOR UPDATE and consistent ordering
        var ownerships = await _dbContext.WalletOwnerships
            .FromSqlRaw(
                @"SELECT * FROM identity.wallet_ownership
                WHERE wallet_id = {0} AND is_deleted = false
                ORDER BY id ASC
                FOR UPDATE",
                walletId.Value.ToString())
            .ToListAsync(cancellationToken);

        return ownerships;
    }

    public async Task<Result<Unit, Error>> RevokeOwnershipAsync(
        WalletOwnershipId ownershipId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var ownership = await _dbContext.WalletOwnerships
                .FirstOrDefaultAsync(o => o.Id == ownershipId, cancellationToken);

            if (ownership == null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<Unit, Error>(
                    Error.NotFound($"Ownership {ownershipId} not found", "WALLET.OWNERSHIP.NOT_FOUND"));
            }

            var result = ownership.UpdateStatus(OwnershipStatus.Revoked, reason);
            if (result.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<Unit, Error>(result.Error);
            }

            // Clear any default wallets that reference this ownership
            if (ownership.IsVerifiedSigning)
            {
                await ClearDefaultWalletsForOwnershipAsync(ownership, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Revoked ownership {OwnershipId} for principal {PrincipalId} on wallet {WalletId}. Reason: {Reason}",
                ownershipId, ownership.PrincipalId, ownership.WalletId, reason);

            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogError(ex,
                "Error revoking ownership {OwnershipId}",
                ownershipId);

            throw;
        }
    }

    public async Task<Result<bool, Error>> CanSetAsDefaultAsync(
        AxonUserId principalId,
        WalletId walletId,
        CancellationToken cancellationToken = default)
    {
        var ownership = await _dbContext.WalletOwnerships
            .AsNoTracking()
            .FirstOrDefaultAsync(o =>
                o.PrincipalId == principalId &&
                o.WalletId == walletId &&
                !o.IsDeleted,
                cancellationToken);

        if (ownership == null)
        {
            return Result.Success<bool, Error>(false);
        }

        // Can only set as default if verified+signing
        if (!ownership.IsVerifiedSigning)
        {
            _logger.LogWarning(
                "Attempted to check default eligibility for non-verified or watch-only ownership. " +
                "PrincipalId: {PrincipalId}, WalletId: {WalletId}, Status: {Status}, AccessMode: {AccessMode}",
                principalId, walletId, ownership.Status, ownership.AccessMode);

            return Result.Success<bool, Error>(false);
        }

        return Result.Success<bool, Error>(true);
    }

    public async Task<Result<IReadOnlyList<WalletOwnership>, Error>> VerifyBatchWalletOwnershipsAsync(
        IEnumerable<(WalletId WalletId, AxonUserId PrincipalId, AccessMode AccessMode, VerificationSource VerificationSource)> requests,
        CancellationToken cancellationToken = default)
    {
        var orderedRequests = OrderWalletRequestsConsistently(requests);
        var retryCount = 0;

        while (retryCount < MaxRetryAttempts)
        {
            try
            {
                return await ExecuteBatchVerificationTransactionAsync(orderedRequests, cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                retryCount++;
                _logger.LogWarning(
                    "Unique constraint violation during batch wallet verification. Attempt {RetryCount}/{MaxRetryAttempts}",
                    retryCount, MaxRetryAttempts);

                if (retryCount >= MaxRetryAttempts)
                {
                    return Result.Failure<IReadOnlyList<WalletOwnership>, Error>(
                        Error.Conflict("Failed to verify batch ownership after retries due to concurrent modifications",
                            "WALLET.VERIFICATION.BATCH_RACE_CONDITION"));
                }

                // Small delay before retry
                await Task.Delay(100 * retryCount, cancellationToken);
            }
        }

        return Result.Failure<IReadOnlyList<WalletOwnership>, Error>(
            Error.Failure("Unexpected error during batch verification", "WALLET.VERIFICATION.BATCH_UNEXPECTED"));
    }

    private async Task ClearDefaultWalletsForOwnershipAsync(
        WalletOwnership ownership,
        CancellationToken cancellationToken)
    {
        // Find and clear any chain defaults that reference this wallet
        var defaults = await _dbContext.PrincipalChainDefaults
            .Where(d => d.PrincipalId == ownership.PrincipalId &&
                       d.WalletId == ownership.WalletId)
            .ToListAsync(cancellationToken);

        foreach (var defaultEntry in defaults)
        {
            _dbContext.PrincipalChainDefaults.Remove(defaultEntry);

            _logger.LogInformation(
                "Cleared default wallet for principal {PrincipalId} on network {NetworkEnvironment} chain {ChainId}",
                ownership.PrincipalId, defaultEntry.NetworkEnvironment, defaultEntry.ChainId);
        }
    }

    /// <summary>
    /// Orders wallet requests consistently by wallet ID to prevent deadlocks.
    /// All multi-wallet operations must use this ordering to avoid transaction deadlocks.
    /// </summary>
    private static IEnumerable<(WalletId WalletId, AxonUserId PrincipalId, AccessMode AccessMode, VerificationSource VerificationSource)>
        OrderWalletRequestsConsistently(
            IEnumerable<(WalletId WalletId, AxonUserId PrincipalId, AccessMode AccessMode, VerificationSource VerificationSource)> requests)
    {
        return requests.OrderBy(r => r.WalletId.Value);
    }

    /// <summary>
    /// Orders wallet IDs consistently to prevent deadlocks in multi-wallet operations.
    /// </summary>
    private static IEnumerable<WalletId> OrderWalletIdsConsistently(IEnumerable<WalletId> walletIds)
    {
        return walletIds.OrderBy(id => id.Value);
    }

    private async Task<Result<IReadOnlyList<WalletOwnership>, Error>> ExecuteBatchVerificationTransactionAsync(
        IEnumerable<(WalletId WalletId, AxonUserId PrincipalId, AccessMode AccessMode, VerificationSource VerificationSource)> orderedRequests,
        CancellationToken cancellationToken)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var results = new List<WalletOwnership>();
            var uniqueWalletIds = orderedRequests.Select(r => r.WalletId).Distinct().ToList();

            // Step 1: Lock all wallets in consistent order to prevent deadlocks
            var wallets = await LockWalletsInOrderAsync(uniqueWalletIds, cancellationToken);
            var missingWallets = uniqueWalletIds.Except(wallets.Select(w => w.Id)).ToList();

            if (missingWallets.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<IReadOnlyList<WalletOwnership>, Error>(
                    Error.NotFound($"Wallets not found: {string.Join(", ", missingWallets.Select(w => w.Value))}",
                        "WALLET.BATCH.NOT_FOUND"));
            }

            // Step 2: Lock all competing ownerships for all wallets in consistent order
            var allOwnerships = await LockBatchCompetingOwnershipsAsync(uniqueWalletIds, cancellationToken);

            // Step 3: Process each verification request
            foreach (var request in orderedRequests)
            {
                var walletOwnerships = allOwnerships.Where(o => o.WalletId == request.WalletId).ToList();

                // Check for existing verified+signing ownership
                var verifiedSigningOwnership = walletOwnerships
                    .FirstOrDefault(o => o.Status == OwnershipStatus.Verified &&
                                       o.AccessMode == AccessMode.Signing &&
                                       !o.IsDeleted);

                // If another principal already has verified+signing, return 409 Conflict
                if (verifiedSigningOwnership != null && verifiedSigningOwnership.PrincipalId != request.PrincipalId)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure<IReadOnlyList<WalletOwnership>, Error>(
                        Error.Conflict(
                            $"Wallet {request.WalletId.Value} already has verified signing ownership by another principal",
                            "WALLET.BATCH.OWNERSHIP.ALREADY_VERIFIED"));
                }

                // Find or create ownership for the requesting principal
                var candidateOwnership = walletOwnerships
                    .FirstOrDefault(o => o.PrincipalId == request.PrincipalId && !o.IsDeleted);

                if (candidateOwnership == null)
                {
                    candidateOwnership = WalletOwnership.Create(
                        request.PrincipalId,
                        request.WalletId,
                        request.AccessMode,
                        OwnershipStatus.Verified,
                        request.VerificationSource);

                    await _dbContext.WalletOwnerships.AddAsync(candidateOwnership, cancellationToken);
                    allOwnerships.Add(candidateOwnership); // Add to tracking collection
                }
                else
                {
                    // Update existing ownership to verified
                    var updateResult = candidateOwnership.UpdateStatus(OwnershipStatus.Verified);
                    if (updateResult.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result.Failure<IReadOnlyList<WalletOwnership>, Error>(updateResult.Error);
                    }

                    // Update access mode if different
                    if (candidateOwnership.AccessMode != request.AccessMode)
                    {
                        var modeResult = candidateOwnership.UpdateAccessMode(request.AccessMode);
                        if (modeResult.IsFailure)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            return Result.Failure<IReadOnlyList<WalletOwnership>, Error>(modeResult.Error);
                        }
                    }
                }

                // Auto-revoke other pending/unverified ownerships on the same wallet (silent operation)
                if (request.AccessMode == AccessMode.Signing)
                {
                    foreach (var ownership in walletOwnerships)
                    {
                        if (ownership.Id != candidateOwnership.Id &&
                            ownership.Status != OwnershipStatus.Verified &&
                            !ownership.IsDeleted)
                        {
                            var revokeResult = ownership.UpdateStatus(
                                OwnershipStatus.Revoked,
                                "Auto-revoked due to batch verified signing ownership");

                            if (revokeResult.IsSuccess)
                            {
                                _logger.LogInformation(
                                    "Auto-revoked pending ownership {OwnershipId} for principal {PrincipalId} on wallet {WalletId} during batch verification",
                                    ownership.Id, ownership.PrincipalId, request.WalletId);
                            }
                        }
                    }
                }

                results.Add(candidateOwnership);
            }

            // Save changes and commit transaction
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully completed batch verification for {RequestCount} requests across {WalletCount} wallets",
                orderedRequests.Count(), uniqueWalletIds.Count);

            return Result.Success<IReadOnlyList<WalletOwnership>, Error>(results.AsReadOnly());
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogError(ex,
                "Error during batch wallet ownership verification for {RequestCount} requests",
                orderedRequests.Count());

            throw;
        }
    }

    private async Task<List<Wallet>> LockWalletsInOrderAsync(
        IEnumerable<WalletId> walletIds,
        CancellationToken cancellationToken)
    {
        var orderedIds = OrderWalletIdsConsistently(walletIds).ToList();

        if (orderedIds.Count == 0)
            return new List<Wallet>();

        if (orderedIds.Count == 1)
        {
            // Use raw SQL for explicit row-level lock with FOR UPDATE
            var singleWallet = await _dbContext.Wallets
                .FromSqlRaw(
                    "SELECT * FROM identity.wallet WHERE id = {0} FOR UPDATE",
                    orderedIds[0].Value.ToString())
                .ToListAsync(cancellationToken);
            return singleWallet;
        }

        // For multiple wallets, we need to use a different approach
        // Build parameterized query for multiple IDs
        var parameters = orderedIds.Select((id, index) => new { Parameter = $"@p{index}", Value = id.Value.ToString() }).ToList();
        var parameterList = string.Join(", ", parameters.Select(p => p.Parameter));
        var sql = $"SELECT * FROM identity.wallet WHERE id IN ({parameterList}) ORDER BY id ASC FOR UPDATE";

        var parameterValues = parameters.Select(p => (object)p.Value).ToArray();

        var wallets = await _dbContext.Wallets
            .FromSqlRaw(sql, parameterValues)
            .ToListAsync(cancellationToken);

        return wallets;
    }

    private async Task<List<WalletOwnership>> LockBatchCompetingOwnershipsAsync(
        IEnumerable<WalletId> walletIds,
        CancellationToken cancellationToken)
    {
        var orderedIds = OrderWalletIdsConsistently(walletIds).ToList();

        if (orderedIds.Count == 0)
            return new List<WalletOwnership>();

        if (orderedIds.Count == 1)
        {
            // Use raw SQL for explicit row-level lock with FOR UPDATE and consistent ordering
            var singleOwnerships = await _dbContext.WalletOwnerships
                .FromSqlRaw(
                    @"SELECT * FROM identity.wallet_ownership
                    WHERE wallet_id = {0} AND is_deleted = false
                    ORDER BY id ASC
                    FOR UPDATE",
                    orderedIds[0].Value.ToString())
                .ToListAsync(cancellationToken);
            return singleOwnerships;
        }

        // For multiple wallets, build parameterized query
        var parameters = orderedIds.Select((id, index) => new { Parameter = $"@p{index}", Value = id.Value.ToString() }).ToList();
        var parameterList = string.Join(", ", parameters.Select(p => p.Parameter));
        var sql = $@"SELECT * FROM identity.wallet_ownership
                WHERE wallet_id IN ({parameterList}) AND is_deleted = false
                ORDER BY wallet_id ASC, id ASC
                FOR UPDATE";

        var parameterValues = parameters.Select(p => (object)p.Value).ToArray();

        var ownerships = await _dbContext.WalletOwnerships
            .FromSqlRaw(sql, parameterValues)
            .ToListAsync(cancellationToken);

        return ownerships;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // PostgreSQL unique constraint violation error code is 23505
        return ex.InnerException?.Message?.Contains("23505", StringComparison.Ordinal) == true ||
               ex.InnerException?.Message?.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true;
    }
}