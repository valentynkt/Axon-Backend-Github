using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Commands.ExchangeCredential;

/// <summary>
/// Handler for ExchangeCredentialCommand that implements the complete exchange flow:
/// 1. Validates and normalizes input data
/// 2. Resolves or creates AxonPrincipal using credential lookup
/// 3. Batch ensures wallets exist to prevent N+1 queries
/// 4. Links wallet ownership with verified and signing status
/// 5. Applies chain defaults with verified-first logic
/// 6. Handles conflicts with proper 409 responses
/// 7. Returns stable operation metrics
/// All operations occur within a single database transaction for atomicity.
/// </summary>
public sealed class ExchangeCredentialHandler : BaseIdentityCommandHandler<ExchangeCredentialCommand, ExchangeOutcome>
{
    private readonly IAxonPrincipalWriteRepository _principalRepository;
    private readonly IWalletWriteRepository _walletRepository;

    public ExchangeCredentialHandler(
        ICurrentUserService currentUserService,
        IAxonPrincipalWriteRepository principalRepository,
        IWalletWriteRepository walletRepository) 
        : base(currentUserService)
    {
        _principalRepository = principalRepository;
        _walletRepository = walletRepository;
    }

    public override async Task<Result<ExchangeOutcome, Error>> Handle(
        ExchangeCredentialCommand command, 
        CancellationToken cancellationToken)
    {
        // Validate input data structure
        var validationResult = await ValidateCommand(command);
        if (validationResult.IsFailure)
            return validationResult.Error;

        try
        {
            // Execute all operations within a single transaction
            return await ExecuteExchangeTransaction(command.UserData, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<ExchangeOutcome, Error>(
                Error.Internal($"Unexpected error during exchange: {ex.Message}", "EXCHANGE.INTERNAL"));
        }
    }

    private async Task<Result<ExchangeOutcome, Error>> ExecuteExchangeTransaction(
        ExchangeUserData userData,
        CancellationToken cancellationToken)
    {
        return await _principalRepository.UnitOfWork.ExecuteInTransactionAsync<Result<ExchangeOutcome, Error>>(async (transactionCt) =>
        {
            // Step 1: Create normalized credential from Dynamic data
            var credentialResult = CreateDynamicCredential(userData);
            if (credentialResult.IsFailure)
                return credentialResult.Error;

            var (providerType, issuer, subject) = credentialResult.Value;

            // Step 2: Find or create Principal
            var principalResult = await ResolveOrCreatePrincipal(
                providerType, issuer, subject, transactionCt);
            if (principalResult.IsFailure)
                return principalResult.Error;

            var (principal, isNewPrincipal) = principalResult.Value;

            // Step 3: Process wallets in batch to prevent N+1 queries
            var walletProcessingResult = await ProcessWalletsBatch(
                principal, userData.Wallets, transactionCt);
            if (walletProcessingResult.IsFailure)
                return walletProcessingResult.Error;

            var walletMetrics = walletProcessingResult.Value;

            // Step 4: Apply verified-first chain defaults
            var defaultsApplied = await ApplyChainDefaults(principal, walletMetrics.ProcessedWalletIds, cancellationToken);

            // Step 5: Persist changes
            if (isNewPrincipal)
            {
                await _principalRepository.AddAsync(principal, cancellationToken);
            }
            else
            {
                await _principalRepository.UpdateAsync(principal, cancellationToken);
            }

            await _principalRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            // Step 6: Return stable metrics
            return Result.Success<ExchangeOutcome, Error>(new ExchangeOutcome(
                AxonId: principal.Id,
                Created: isNewPrincipal,
                WalletsProcessed: walletMetrics.Processed,
                WalletsLinked: walletMetrics.Linked,
                DefaultsApplied: defaultsApplied,
                Skipped: walletMetrics.Skipped,
                Conflicts: walletMetrics.Conflicts
            ));
        }, cancellationToken);
    }

    private static async Task<Result<ExchangeOutcome, Error>> ValidateCommand(ExchangeCredentialCommand command)
    {
        if (command.UserData is null)
            return Result.Failure<ExchangeOutcome, Error>(
                Error.Validation("User data is required", "EXCHANGE.USER_DATA_REQUIRED"));

        if (string.IsNullOrWhiteSpace(command.UserData.UserId))
            return Result.Failure<ExchangeOutcome, Error>(
                Error.Validation("User ID is required", "EXCHANGE.USER_ID_REQUIRED"));

        if (string.IsNullOrWhiteSpace(command.UserData.EnvironmentId))
            return Result.Failure<ExchangeOutcome, Error>(
                Error.Validation("Environment ID is required", "EXCHANGE.ENV_ID_REQUIRED"));

        await Task.CompletedTask;
        return Result.Success<ExchangeOutcome, Error>(default!);
    }

    private static Result<(ProviderType providerType, string issuer, string subject), Error> CreateDynamicCredential(
        ExchangeUserData userData)
    {
        // Create Dynamic provider type
        var providerResult = ProviderType.Create("dynamic");
        if (providerResult.IsFailure)
            return Result.Failure<(ProviderType, string, string), Error>(providerResult.Error);

        // Use environment ID as issuer and user ID as subject for Dynamic
        var issuer = $"dynamic:{userData.EnvironmentId}";
        var subject = userData.UserId;

        return Result.Success<(ProviderType, string, string), Error>((providerResult.Value, issuer, subject));
    }

    private async Task<Result<(AxonPrincipal principal, bool isNew), Error>> ResolveOrCreatePrincipal(
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken cancellationToken)
    {
        // First, try to find existing principal by credential
        var existingPrincipal = await _principalRepository.FindByCredentialAsync(
            providerType, issuer, subject, cancellationToken);

        if (existingPrincipal != null)
        {
            return Result.Success<(AxonPrincipal, bool), Error>((existingPrincipal, false));
        }

        // Create new principal with Dynamic credential
        var createResult = AxonPrincipal.CreateWithDynamicCredential(
            providerType, 
            issuer, 
            subject);

        if (createResult.IsFailure)
        {
            return Result.Failure<(AxonPrincipal, bool), Error>(createResult.Error);
        }

        return Result.Success<(AxonPrincipal, bool), Error>((createResult.Value, true));
    }

    private async Task<Result<WalletProcessingMetrics, Error>> ProcessWalletsBatch(
        AxonPrincipal principal,
        List<ExchangeWalletData> wallets,
        CancellationToken cancellationToken)
    {
        if (wallets is null || wallets.Count == 0)
        {
            return Result.Success<WalletProcessingMetrics, Error>(
                new WalletProcessingMetrics(0, 0, 0, 0, new List<WalletId>()));
        }

        // Step 1: Parse wallet data and create address pairs for batch lookup
        var walletSpecs = new List<(string chainId, Address address)>();
        var parseErrors = new List<string>();

        foreach (var wallet in wallets)
        {
            var addressResult = Address.Create(wallet.Address);
            if (addressResult.IsFailure)
            {
                parseErrors.Add($"Invalid address {wallet.Address}: {addressResult.Error.Message}");
                continue;
            }

            walletSpecs.Add((wallet.Chain, addressResult.Value));
        }

        if (parseErrors.Count > 0)
        {
            return Result.Failure<WalletProcessingMetrics, Error>(
                Error.Validation($"Address parsing errors: {string.Join(", ", parseErrors)}", "EXCHANGE.INVALID_ADDRESSES"));
        }

        // Step 2: Batch ensure wallets exist using principal repository method
        var walletLookup = await _principalRepository.EnsureManyByChainAndAddressAsync(walletSpecs, cancellationToken);
        var walletIds = walletLookup.Values.ToList();

        // Step 3: Check for existing verified signing ownership conflicts
        var existingOwners = await _principalRepository.FindVerifiedSigningOwnersAsync(walletIds, cancellationToken);
        
        var conflicts = existingOwners.Where(kvp => kvp.Value.Id != principal.Id).ToList();
        if (conflicts.Count > 0)
        {
            // Return conflict information for 409 response
            var conflictWallet = conflicts.First();
            var conflictSpec = walletSpecs.First(ws => walletLookup.ContainsKey(ws) && walletLookup[ws] == conflictWallet.Key);
            
            return Result.Failure<WalletProcessingMetrics, Error>(
                IdentityDomainErrors.Wallet.WalletOwnershipConflict(conflictSpec.chainId, conflictSpec.address.Value));
        }

        // Step 4: Link wallets with verified & signing ownership
        var linked = 0;
        var skipped = 0;

        foreach (var walletId in walletIds)
        {
            // Create wallet ownership entity
            var ownership = WalletOwnership.Create(
                principal.Id, 
                walletId, 
                Domain.Enums.AccessMode.Signing, 
                Domain.Enums.OwnershipStatus.Verified);

            // Check function that verifies no other principal owns this wallet with verified+signing
            var checkExistingOwnership = (WalletId wId, Domain.Enums.AccessMode mode, Domain.Enums.OwnershipStatus status) =>
            {
                // If this wallet is already in the existingOwners collection and not owned by current principal, it's a conflict
                var hasConflict = existingOwners.ContainsKey(wId) && existingOwners[wId].Id != principal.Id;
                return Result.Success<bool, Error>(hasConflict);
            };

            var linkResult = principal.LinkWalletOwnership(ownership, checkExistingOwnership);
            if (linkResult.IsSuccess)
            {
                linked++;
            }
            else
            {
                skipped++;
            }
        }

        return Result.Success<WalletProcessingMetrics, Error>(
            new WalletProcessingMetrics(wallets.Count, linked, skipped, 0, walletIds));
    }

    /// <summary>
    /// Applies chain defaults for verified and signing wallets in verified-first order.
    /// Returns the count of actual defaults applied (excluding no-ops).
    /// </summary>
    private async Task<int> ApplyChainDefaults(
        AxonPrincipal principal, 
        List<WalletId> processedWalletIds, 
        CancellationToken cancellationToken)
    {
        // Get eligible verified & signing ownerships from processed wallets
        var eligibleOwnerships = principal.WalletOwnerships
            .Where(wo => processedWalletIds.Contains(wo.WalletId) && 
                        wo.Status == Domain.Enums.OwnershipStatus.Verified && 
                        wo.AccessMode == Domain.Enums.AccessMode.Signing)
            .ToList();
            
        if (eligibleOwnerships.Count == 0)
            return 0;
            
        // Get wallet details to determine chain mappings
        var wallets = await _walletRepository.GetByIdsAsync(
            eligibleOwnerships.Select(o => o.WalletId), 
            cancellationToken);
            
        // Group wallets by chain ID and apply verified-first wallet as default per chain
        var chainGroups = wallets
            .GroupBy(w => w.ChainId)
            .ToList();
            
        int defaultsApplied = 0;
        
        foreach (var chainGroup in chainGroups)
        {
            var chainId = chainGroup.Key;
            
            // Get the first verified+signing wallet for this chain (order by ID for deterministic behavior)
            var firstWallet = chainGroup
                .OrderBy(w => w.Id.Value)
                .First();
                
            // Check current default before applying to detect no-ops
            var currentDefault = principal.GetDefaultWalletForChain(chainId);
            
            // Apply chain default using domain method
            var result = principal.ApplyChainDefault(chainId, firstWallet.Id);
            
            // Count only successful applications that weren't no-ops
            if (result.IsSuccess && currentDefault != firstWallet.Id)
            {
                defaultsApplied++;
            }
        }
        
        return defaultsApplied;
    }

    /// <summary>
    /// Metrics for wallet processing operations
    /// </summary>
    private sealed record WalletProcessingMetrics(
        int Processed,
        int Linked,
        int Skipped,
        int Conflicts,
        List<WalletId> ProcessedWalletIds);
}