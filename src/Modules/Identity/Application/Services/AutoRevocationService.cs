using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Events;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Implementation of auto-revocation service for handling exclusive ownership constraints.
/// This service ensures that when a principal gains verified+signing ownership of a wallet,
/// all other principals' pending ownerships are automatically revoked.
/// </summary>
public sealed class AutoRevocationService : IAutoRevocationService
{
    private readonly IIdentityDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AutoRevocationService> _logger;

    public AutoRevocationService(
        IIdentityDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<AutoRevocationService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<int, Error>> ProcessAutoRevocationAsync(
        WalletId walletId,
        AxonUserId excludePrincipalId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Processing auto-revocation for wallet {WalletId} excluding principal {PrincipalId}",
                walletId, excludePrincipalId);

            // Find all principals with pending ownership of this wallet (excluding the winner)
            var principalsWithPendingOwnership = await _dbContext.Principals
                .Include(p => p.WalletOwnerships)
                .Where(p => p.Id != excludePrincipalId &&
                           p.WalletOwnerships.Any(wo => wo.WalletId == walletId &&
                                                       wo.Status == OwnershipStatus.Pending &&
                                                       !wo.IsDeleted))
                .ToListAsync(cancellationToken);

            _logger.LogDebug(
                "Found {Count} principals with pending ownership for wallet {WalletId}",
                principalsWithPendingOwnership.Count, walletId);

            int totalRevoked = 0;

            // Revoke pending ownerships through the aggregate method
            foreach (var principal in principalsWithPendingOwnership)
            {
                var revokeResult = principal.RevokePendingOwnershipsForWallet(
                    walletId,
                    _timeProvider,
                    "Auto-revoked due to exclusivity constraint");

                if (revokeResult.IsFailure)
                {
                    _logger.LogWarning(
                        "Failed to revoke ownerships for principal {PrincipalId} on wallet {WalletId}: {Error}",
                        principal.Id, walletId, revokeResult.Error);
                    continue;
                }

                totalRevoked += revokeResult.Value;

                _logger.LogDebug(
                    "Revoked {Count} pending ownerships for principal {PrincipalId} on wallet {WalletId}",
                    revokeResult.Value, principal.Id, walletId);
            }

            // IMPORTANT: Do NOT call SaveChangesAsync here
            // The caller is responsible for saving changes to ensure all operations
            // participate in the same transaction/unit of work

            _logger.LogInformation(
                "Auto-revocation prepared for wallet {WalletId}. Total to be revoked: {TotalRevoked}",
                walletId, totalRevoked);

            return Result.Success<int, Error>(totalRevoked);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing auto-revocation for wallet {WalletId}",
                walletId);

            return Result.Failure<int, Error>(Error.Internal(
                $"Failed to process auto-revocation for wallet {walletId}: {ex.Message}",
                "AutoRevocation.ProcessingError",
                ex));
        }
    }

    public async Task<Result<int, Error>> ProcessAutoRevocationFromEventsAsync(
        AxonUserId principalId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Load the principal with its domain events
            var principal = await _dbContext.Principals
                .Include(p => p.WalletOwnerships)
                .FirstOrDefaultAsync(p => p.Id == principalId, cancellationToken);

            if (principal == null)
            {
                return Result.Failure<int, Error>(Error.NotFound(
                    $"Principal {principalId} not found",
                    "AutoRevocation.PrincipalNotFound"));
            }

            // Check for verified+signing ownership events that require auto-revocation
            var walletIdsRequiringRevocation = principal.DomainEvents
                .OfType<OwnershipChangedEvent>()
                .Where(evt => evt.ChangeType == "verified_signing_added" &&
                             evt.Metadata != null &&
                             evt.Metadata.ContainsKey("RequiresAutoRevocation") &&
                             evt.Metadata["RequiresAutoRevocation"] == "true")
                .Select(evt => evt.WalletId)
                .Distinct()
                .ToList();

            if (walletIdsRequiringRevocation.Count == 0)
            {
                _logger.LogDebug(
                    "No auto-revocation required for principal {PrincipalId}",
                    principalId);
                return Result.Success<int, Error>(0);
            }

            int totalRevoked = 0;

            // Process auto-revocation for each wallet
            foreach (var walletId in walletIdsRequiringRevocation)
            {
                var result = await ProcessAutoRevocationAsync(
                    walletId,
                    principalId,
                    cancellationToken);

                if (result.IsSuccess)
                {
                    totalRevoked += result.Value;
                }
            }

            return Result.Success<int, Error>(totalRevoked);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing auto-revocation from events for principal {PrincipalId}",
                principalId);

            return Result.Failure<int, Error>(Error.Internal(
                $"Failed to process auto-revocation from events: {ex.Message}",
                "AutoRevocation.EventProcessingError",
                ex));
        }
    }
}