using System.Diagnostics;
using System.Globalization;
using Axon.Modules.Identity.Application.Common.Models;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Services;

public sealed class PrincipalResolutionService : IPrincipalResolutionService
{
    private static readonly ActivitySource ActivitySource = new("Axon.Identity.PrincipalResolution");

    private readonly IAxonPrincipalReadRepository _principalRepository;
    private readonly IAxonPrincipalWriteRepository _principalWriteRepository;
    private readonly IWalletReadRepository _walletReadRepository;
    private readonly IWalletWriteRepository _walletWriteRepository;
    private readonly IWalletOwnershipRepository _ownershipRepository;
    private readonly IAutoRevocationService _autoRevocationService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PrincipalResolutionService> _logger;

    public PrincipalResolutionService(
        IAxonPrincipalReadRepository principalRepository,
        IAxonPrincipalWriteRepository principalWriteRepository,
        IWalletReadRepository walletReadRepository,
        IWalletWriteRepository walletWriteRepository,
        IWalletOwnershipRepository ownershipRepository,
        IAutoRevocationService autoRevocationService,
        TimeProvider timeProvider,
        ILogger<PrincipalResolutionService> logger)
    {
        _principalRepository = principalRepository;
        _principalWriteRepository = principalWriteRepository;
        _walletReadRepository = walletReadRepository;
        _walletWriteRepository = walletWriteRepository;
        _ownershipRepository = ownershipRepository;
        _autoRevocationService = autoRevocationService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<PrincipalResolutionResult, Error>> ResolveAsync(
        ProviderType provider,
        string issuer,
        string subject,
        ChainId chainId,
        Address address,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("PrincipalResolution.Resolve");
        activity?.SetTag("provider", provider.ToString(CultureInfo.InvariantCulture));
        activity?.SetTag("chain_id", chainId.Value);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Step 1: Credential-first resolution
            var credentialResult = await ResolveByCredentialAsync(
                provider, issuer, subject, chainId, address, cancellationToken);

            if (credentialResult.IsSuccess)
            {
                LogResolution("credential", chainId, address, stopwatch.ElapsedMilliseconds);
                return credentialResult;
            }

            // Step 2: Wallet-fallback resolution with tie-breaking
            var walletResult = await ResolveByWalletAsync(
                chainId, address, cancellationToken);

            if (walletResult.IsSuccess)
            {
                LogResolution("wallet", chainId, address, stopwatch.ElapsedMilliseconds);
                return walletResult;
            }

            // Step 3: Create new principal with race protection
            var createResult = await CreateNewPrincipalWithRaceProtectionAsync(
                chainId, address, cancellationToken);

            LogResolution("created", chainId, address, stopwatch.ElapsedMilliseconds);
            return createResult;
        }
        finally
        {
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task<Result<PrincipalResolutionResult, Error>> ResolveByCredentialAsync(
        ProviderType provider,
        string issuer,
        string subject,
        ChainId chainId,
        Address address,
        CancellationToken cancellationToken)
    {
        _ = chainId; // Still passed for method signature compatibility
        _ = address; // Still passed for method signature compatibility

        using var activity = ActivitySource.StartActivity("PrincipalResolution.CredentialFirst");

        var principal = await _principalRepository.FindByCredentialAsync(
            provider, issuer, subject, cancellationToken);

        if (principal is null)
        {
            return Result.Failure<PrincipalResolutionResult, Error>(
                Error.NotFound("No principal found with given credentials"));
        }

        // Return the principal found by credential
        return Result.Success<PrincipalResolutionResult, Error>(new PrincipalResolutionResult(
            principal, ResolutionPath.Credential, false));
    }

    private async Task<Result<PrincipalResolutionResult, Error>> ResolveByWalletAsync(
        ChainId chainId,
        Address address,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("PrincipalResolution.WalletFallback");

        // MUST use triple-key lookup
        var wallet = await _walletReadRepository.FindWalletAsync(
            chainId, address, cancellationToken);

        if (wallet is null)
        {
            return Result.Failure<PrincipalResolutionResult, Error>(
                Error.NotFound("No wallet found for resolution"));
        }

        // Get active ownerships (status != revoked) from wallet candidates
        var ownerships = await _ownershipRepository.FindActiveOwnershipsByWalletAsync(
            wallet.Id, cancellationToken);

        if (!ownerships.Any())
        {
            return Result.Failure<PrincipalResolutionResult, Error>(
                Error.NotFound("No active ownerships for wallet"));
        }

        // Check if verified+signing exists
        var verifiedSigning = ownerships
            .FirstOrDefault(o => o.Ownership.Status == OwnershipStatus.Verified && o.Ownership.AccessMode == AccessMode.Signing);

        if (verifiedSigning is not null)
        {
            return Result.Success<PrincipalResolutionResult, Error>(new PrincipalResolutionResult(
                verifiedSigning.Principal, ResolutionPath.Wallet, WasAutoLinked: false));
        }

        // Apply tie-break rules ONLY when no verified+signing exists
        var winner = ApplyTieBreakRules(ownerships);

        // Auto-revoke losers through their respective aggregates
        foreach (var ownershipWithPrincipal in ownerships.Where(o => o.Ownership.Id != winner.Ownership.Id))
        {
            _logger.LogInformation(
                "Auto-revoking ownership {OwnershipId} reason=conflict_lost for {ChainId}:{Address}",
                ownershipWithPrincipal.Ownership.Id, chainId.Value, address.Value);

            // Load the principal aggregate and revoke through domain method
            var principal = await _principalWriteRepository.GetByIdAsync(ownershipWithPrincipal.Ownership.PrincipalId, cancellationToken);
            if (principal != null)
            {
                var revokeResult = principal.UpdateWalletOwnershipStatus(
                    ownershipWithPrincipal.Ownership.WalletId,
                    OwnershipStatus.Revoked,
                    _timeProvider,
                    "conflict_lost");

                if (revokeResult.IsSuccess)
                {
                    await _principalWriteRepository.UpdateAsync(principal, cancellationToken);
                }
            }
        }

        return Result.Success<PrincipalResolutionResult, Error>(new PrincipalResolutionResult(
            winner.Principal, ResolutionPath.Wallet, WasAutoLinked: false));
    }

    private static WalletOwnershipWithPrincipal ApplyTieBreakRules(IEnumerable<WalletOwnershipWithPrincipal> ownerships)
    {
        return ownerships
            .OrderBy(o => GetVerificationSourceRank(o.Ownership.VerificationSource))
            .ThenBy(o => o.Principal.CreatedAt)
            .First();
    }

    private static int GetVerificationSourceRank(VerificationSource source) => source switch
    {
        VerificationSource.DynamicAttested => 1,
        VerificationSource.DirectSignatureMsg => 2,
        VerificationSource.DirectSignatureTx => 3,
        VerificationSource.WatchOnly => 4,
        _ => 99
    };

    private async Task<Result<PrincipalResolutionResult, Error>> CreateNewPrincipalWithRaceProtectionAsync(
        ChainId chainId,
        Address address,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("PrincipalResolution.CreateNew");

        // Upsert wallet first to prevent race conditions
        var wallet = await _walletWriteRepository.UpsertWalletAsync(
            chainId, address, cancellationToken);

        // Use wallet directly - no need to re-read (eliminates RAW consistency issue)
        // The wallet is already persisted and has the correct ID after SaveChangesAsync()
        var ownerships = await _ownershipRepository.FindActiveOwnershipsByWalletAsync(
            wallet.Id, cancellationToken);

        // Check if another request already created principal during race
        if (ownerships.Any())
        {
            var winner = ownerships[0];
            _logger.LogInformation(
                "Race condition detected: Using existing principal {PrincipalId} created by concurrent request",
                winner.Ownership.PrincipalId);

            return Result.Success<PrincipalResolutionResult, Error>(new PrincipalResolutionResult(
                winner.Principal, ResolutionPath.Wallet, WasAutoLinked: false));
        }

        // Safe to create new principal
        var principal = AxonPrincipal.CreateHuman();

        // Create ownership entity and link it to the principal through the aggregate
        var ownership = WalletOwnership.Create(
            principal.Id,
            wallet.Id,
            AccessMode.Signing,
            OwnershipStatus.Verified,
            VerificationSource.DynamicAttested);

        // Define the uniqueness check function for wallet ownership
        Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> checkExistingOwnership =
            (walletId, accessMode, status) =>
            {
                // For this creation scenario, we already checked that no ownerships exist
                // This function is used for conflict detection with other verified+signing owners
                return Result.Success<bool, Error>(false);
            };

        var linkResult = principal.LinkWalletOwnership(ownership, checkExistingOwnership, _timeProvider);
        if (linkResult.IsFailure)
        {
            return Result.Failure<PrincipalResolutionResult, Error>(linkResult.Error);
        }

        await _principalWriteRepository.AddAsync(principal, cancellationToken);

        try
        {
            // Process auto-revocation BEFORE SaveChanges to ensure it's in the same transaction
            // This ensures that when we create a verified+signing ownership, any pending
            // ownerships from other principals are revoked atomically
            if (ownership.IsVerifiedSigning)
            {
                var autoRevocationResult = await _autoRevocationService.ProcessAutoRevocationAsync(
                    wallet.Id,
                    principal.Id,
                    cancellationToken);

                if (autoRevocationResult.IsFailure)
                {
                    _logger.LogWarning(
                        "Auto-revocation preparation failed for wallet {WalletId}: {Error}",
                        wallet.Id, autoRevocationResult.Error);
                    // Continue even if auto-revocation fails - it's not critical for the auth flow
                }
                else if (autoRevocationResult.Value > 0)
                {
                    _logger.LogInformation(
                        "Prepared auto-revocation for {Count} pending ownerships on wallet {WalletId}",
                        autoRevocationResult.Value, wallet.Id);
                }
            }

            // Note: SaveChanges will be called by MediatR's UnitOfWorkBehavior after the entire command completes
            // This ensures all changes (Principal + Wallets + Ownerships + IdentityUser) are committed atomically
            // in a single transaction. Calling SaveChanges here would create a nested transaction and cause
            // duplicate key violations when the provider tries to add the Principal again.

            _logger.LogInformation(
                "Successfully created principal {PrincipalId} with verified+signing ownership of wallet {WalletId}",
                principal.Id, wallet.Id);

            return Result.Success<PrincipalResolutionResult, Error>(new PrincipalResolutionResult(
                principal, ResolutionPath.Created, WasAutoLinked: false));
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
        {
            // Another thread created ownership for this wallet - re-query to get the winner
            _logger.LogInformation(
                "Race condition during ownership creation for wallet {WalletId}, re-querying for existing ownership",
                wallet.Id);

            var existingOwnerships = await _ownershipRepository.FindActiveOwnershipsByWalletAsync(
                wallet.Id, cancellationToken);

            if (existingOwnerships.Any())
            {
                var winner = existingOwnerships[0];
                return Result.Success<PrincipalResolutionResult, Error>(new PrincipalResolutionResult(
                    winner.Principal, ResolutionPath.Wallet, WasAutoLinked: false));
            }

            // This shouldn't happen but handle gracefully
            throw new InvalidOperationException(
                $"Failed to retrieve ownership after race condition for wallet {wallet.Id}");
        }
    }

    private void LogResolution(
        string path,
        ChainId chainId,
        Address address,
        long elapsedMs)
    {
        _logger.LogInformation(
            "Principal resolution completed: resolution_path={ResolutionPath} " +
            "chain_id={ChainId} address={Address} duration_ms={Duration}",
            path, chainId.Value, address.Value, elapsedMs);
    }
}