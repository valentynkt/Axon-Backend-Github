using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.Common.Constants;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Infrastructure.Persistence.Write;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Metrics;

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
    private const string JwtIssuerMetadataKey = "jwt_issuer";
    private readonly IAxonPrincipalWriteRepository _principalWriteRepository;
    private readonly IWalletWriteRepository _walletRepository;
    private readonly IExchangeMetricsService _metricsService;
    private readonly IMemoryCache _memoryCache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ExchangeCredentialHandler> _logger;

    // OpenTelemetry metrics for cache warming monitoring
    private static readonly Meter Meter = new("Axon.Identity.Exchange");
    private static readonly Counter<long> CacheWarmingSuccessCounter =
        Meter.CreateCounter<long>("axon.identity.cache_warming_success", description: "Number of successful cache warming operations");
    private static readonly Counter<long> CacheWarmingFailureCounter =
        Meter.CreateCounter<long>("axon.identity.cache_warming_failures", description: "Number of failed cache warming operations");
    private static readonly Histogram<double> CacheWarmingLatency =
        Meter.CreateHistogram<double>("axon.identity.cache_warming_duration_ms", description: "Cache warming operation latency in milliseconds");

    public ExchangeCredentialHandler(
        ICurrentUserService currentUserService,
        IAxonPrincipalWriteRepository principalRepository,
        IWalletWriteRepository walletRepository,
        IExchangeMetricsService metricsService,
        IMemoryCache memoryCache,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ExchangeCredentialHandler> logger)
        : base(currentUserService)
    {
        _principalWriteRepository = principalRepository;
        _walletRepository = walletRepository;
        _metricsService = metricsService;
        _memoryCache = memoryCache;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public override async Task<Result<ExchangeOutcome, Error>> Handle(
        ExchangeCredentialCommand command,
        CancellationToken cancellationToken)
    {
        var correlationId = Activity.Current?.Id ?? "unknown";
        var processingStartTime = Stopwatch.StartNew();

        // Log exchange start with structured data
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["Provider"] = "dynamic",
            ["UserId"] = command.UserData?.UserId ?? "unknown",
            ["EnvironmentId"] = command.UserData?.EnvironmentId ?? "unknown",
            ["WalletCount"] = command.UserData?.Wallets?.Count ?? 0
        });

        _logger.LogInformation("Exchange credential operation started for user {UserId} with {WalletCount} wallets",
            command.UserData?.UserId, command.UserData?.Wallets?.Count ?? 0);

        // Validate input data structure
        var validationResult = await ValidateCommand(command);
        if (validationResult.IsFailure)
        {
            processingStartTime.Stop();
            _metricsService.RecordExchangeFailure(
                validationResult.Error.Code,
                "validation",
                processingStartTime.ElapsedMilliseconds);

            _logger.LogWarning("Exchange validation failed: {ErrorCode} - {Message}",
                validationResult.Error.Code, validationResult.Error.Message);
            return validationResult.Error;
        }

        try
        {
            // Execute transaction with simple retry pattern
            var result = await ExecuteExchangeTransaction(command.UserData!, cancellationToken);

            processingStartTime.Stop();

            if (result.IsSuccess)
            {
                var outcome = result.Value;
                _metricsService.RecordExchangeSuccess(
                    command.UserData!.UserId,
                    outcome.Created,
                    outcome.WalletsProcessed,
                    outcome.WalletsLinked,
                    outcome.Conflicts,
                    processingStartTime.ElapsedMilliseconds);

                _logger.LogInformation("Exchange completed successfully: Created={Created}, WalletsProcessed={WalletsProcessed}, " +
                    "WalletsLinked={WalletsLinked}, Conflicts={Conflicts}, DefaultsApplied={DefaultsApplied}, Duration={Duration}ms",
                    outcome.Created, outcome.WalletsProcessed, outcome.WalletsLinked,
                    outcome.Conflicts, outcome.DefaultsApplied, processingStartTime.ElapsedMilliseconds);
            }
            else
            {
                _metricsService.RecordExchangeFailure(
                    result.Error.Code,
                    result.Error.Type.ToString().ToLowerInvariant(),
                    processingStartTime.ElapsedMilliseconds);

                _logger.LogWarning("Exchange failed: {ErrorCode} - {Message}",
                    result.Error.Code, result.Error.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            processingStartTime.Stop();
            _metricsService.RecordExchangeFailure("EXCHANGE.INTERNAL", "internal", processingStartTime.ElapsedMilliseconds);

            _logger.LogError(ex, "Unexpected error during exchange processing after {Duration}ms",
                processingStartTime.ElapsedMilliseconds);

            return Result.Failure<ExchangeOutcome, Error>(
                Error.Internal($"Unexpected error during exchange: {ex.Message}", "EXCHANGE.INTERNAL"));
        }
    }

    /// <summary>
    /// Warms both memory cache and request-scoped cache for AxonUserId resolution.
    /// Populates caches with user identity mapping to enable >95% cache hit rate for subsequent requests.
    /// Cache warming failures are handled gracefully and don't disrupt the exchange flow.
    /// </summary>
    /// <param name="dynamicUserId">The Dynamic user ID from JWT token</param>
    /// <param name="axonUserId">The resolved internal AxonUserId</param>
    /// <returns>Task representing the async cache warming operation</returns>
    private async Task WarmUserContextCaches(string dynamicUserId, AxonUserId axonUserId)
    {
        using var activity = Activity.Current?.Source.StartActivity("WarmUserContextCaches");
        activity?.SetTag("dynamic_user_id", dynamicUserId);
        activity?.SetTag("axon_user_id", axonUserId.Value);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Warm memory cache for cross-request access
            var cacheKey = $"axon:user:{dynamicUserId}";
            _memoryCache.Set(cacheKey, axonUserId, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(15),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                Priority = CacheItemPriority.High
            });

            // Warm request-scoped cache for immediate use
            if (_httpContextAccessor.HttpContext != null)
            {
                _httpContextAccessor.HttpContext.Items["AxonUserId"] = axonUserId;
            }

            stopwatch.Stop();

            // Record success metrics
            CacheWarmingSuccessCounter.Add(1);
            CacheWarmingLatency.Record(stopwatch.ElapsedMilliseconds);

            _logger.LogInformation("User context cache warmed: {DynamicUserId} -> {AxonUserId}, Duration: {Duration}ms",
                dynamicUserId, axonUserId.Value, stopwatch.ElapsedMilliseconds);
                
            activity?.SetTag("cache_warming_status", "success");
            activity?.SetTag("cache_warming_duration_ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record failure metrics
            CacheWarmingFailureCounter.Add(1);
            CacheWarmingLatency.Record(stopwatch.ElapsedMilliseconds);

            // Cache warming failures should not break exchange flow
            _logger.LogWarning(ex, "Cache warming failed for user {DynamicUserId}, Duration: {Duration}ms", 
                dynamicUserId, stopwatch.ElapsedMilliseconds);
            activity?.SetTag("cache_warming_status", "failure");
            activity?.SetTag("cache_warming_duration_ms", stopwatch.ElapsedMilliseconds);
        }

        await Task.CompletedTask;
    }

    private async Task<Result<ExchangeOutcome, Error>> ExecuteExchangeTransaction(
        ExchangeUserData userData,
        CancellationToken cancellationToken)
    {
        // Step 1: Create normalized credential from Dynamic data
        var credentialResult = CreateDynamicCredential(userData);
        if (credentialResult.IsFailure)
            return credentialResult.Error;

        var (providerType, issuer, subject) = credentialResult.Value;

        // Step 2: Find or create Principal using wallet-first resolution
        var principalResult = await ResolveOrCreatePrincipalWalletFirst(
            providerType, issuer, subject, userData.Wallets, cancellationToken);
        if (principalResult.IsFailure)
            return principalResult.Error;

        var (principal, isNewPrincipal) = principalResult.Value;

        // Step 3: Process wallets in batch to prevent N+1 queries
        var walletProcessingResult = await ProcessWalletsBatch(
            principal, userData.Wallets, cancellationToken);
        if (walletProcessingResult.IsFailure)
            return walletProcessingResult.Error;

        var walletMetrics = walletProcessingResult.Value;

        // Step 4: Apply verified-first chain defaults
        var defaultsApplied = await ApplyChainDefaults(principal, walletMetrics.ProcessedWalletIds, cancellationToken);

        _logger.LogDebug("Before persistence: PrincipalId={PrincipalId}, DefaultsApplied={DefaultsApplied}, " +
            "ChainDefaultsCount={ChainDefaultsCount}, WalletOwnershipsCount={WalletOwnershipsCount}",
            principal.Id.Value, defaultsApplied, principal.PrincipalChainDefaults.Count, principal.WalletOwnerships.Count);

        // Step 5: Persist changes
        if (isNewPrincipal)
        {
            await _principalWriteRepository.AddAsync(principal, cancellationToken);
            _logger.LogDebug("Principal added to repository (new): PrincipalId={PrincipalId}", principal.Id.Value);
        }
        else
        {
            await _principalWriteRepository.UpdateAsync(principal, cancellationToken);
            _logger.LogDebug("Principal updated in repository (existing): PrincipalId={PrincipalId}", principal.Id.Value);
        }

        var saveResult = await _principalWriteRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("SaveChanges completed: PrincipalId={PrincipalId}, RowsAffected={RowsAffected}, " +
            "DefaultsApplied={DefaultsApplied}, FinalChainDefaultsCount={FinalChainDefaultsCount}",
            principal.Id.Value, saveResult, defaultsApplied, principal.PrincipalChainDefaults.Count);

        // Step 6: Warm user context caches for subsequent identity resolution
        await WarmUserContextCaches(userData.UserId, principal.Id);

        // Step 7: Return stable metrics
        return Result.Success<ExchangeOutcome, Error>(new ExchangeOutcome(
            AxonUserId: principal.Id,
            Created: isNewPrincipal,
            WalletsProcessed: walletMetrics.Processed,
            WalletsLinked: walletMetrics.Linked,
            DefaultsApplied: defaultsApplied,
            Skipped: walletMetrics.Skipped,
            Conflicts: walletMetrics.Conflicts
        ));
    }

    /// <summary>
    /// Validates the exchange command for required fields and structural integrity.
    /// </summary>
    /// <param name="command">The exchange command to validate</param>
    /// <returns>Success result with dummy outcome if validation passes, or failure with validation error</returns>
    /// <remarks>
    /// Validates that UserData, UserId, and EnvironmentId are provided and non-empty.
    /// This method performs structural validation only - business rule validation occurs later in the flow.
    /// </remarks>
    private static async Task<Result<ExchangeOutcome, Error>> ValidateCommand(ExchangeCredentialCommand command)
    {
        if (command.UserData is not { } userData)
            return Result.Failure<ExchangeOutcome, Error>(
                Error.Validation("User data is required", "EXCHANGE.USER_DATA_REQUIRED"));

        if (string.IsNullOrWhiteSpace(userData.UserId))
            return Result.Failure<ExchangeOutcome, Error>(
                Error.Validation("User ID is required", "EXCHANGE.USER_ID_REQUIRED"));

        if (string.IsNullOrWhiteSpace(userData.EnvironmentId))
            return Result.Failure<ExchangeOutcome, Error>(
                Error.Validation("Environment ID is required", "EXCHANGE.ENV_ID_REQUIRED"));

        await Task.CompletedTask;
        return Result.Success<ExchangeOutcome, Error>(new ExchangeOutcome(
            AxonUserId: default!,
            Created: false,
            WalletsProcessed: 0,
            WalletsLinked: 0,
            DefaultsApplied: 0,
            Skipped: 0,
            Conflicts: 0
        ));
    }


    private static Result<(ProviderType providerType, string issuer, string subject), Error> CreateDynamicCredential(
        ExchangeUserData userData)
    {
        // Create Dynamic provider type
        var providerResult = ProviderType.Create("dynamic");
        if (providerResult.IsFailure)
            return Result.Failure<(ProviderType, string, string), Error>(providerResult.Error);

        // Use JWT issuer from metadata if available, otherwise construct using Dynamic's format
        var issuer = userData.AdditionalMetadata?.TryGetValue(JwtIssuerMetadataKey, out var jwtIssuer) == true && jwtIssuer is string jwtIssuerStr
            ? jwtIssuerStr
            : $"{DynamicAuthConstants.IssuerPrefix}/{userData.EnvironmentId}";

        // Log which issuer source was used for debugging
        var issuerSource = userData.AdditionalMetadata?.ContainsKey(JwtIssuerMetadataKey) == true ? "JWT claim" : "constructed";
        System.Diagnostics.Debug.WriteLine($"Using issuer from {issuerSource}: {issuer}");
        var subject = userData.UserId;

        return Result.Success<(ProviderType, string, string), Error>((providerResult.Value, issuer, subject));
    }

    /// <summary>
    /// Resolves or creates a principal using wallet-first resolution strategy.
    /// Prioritizes wallet ownership over credential matching to prevent duplicate identities.
    /// </summary>
    /// <param name="providerType">Authentication provider type</param>
    /// <param name="issuer">Token issuer identifier</param>
    /// <param name="subject">Token subject identifier</param>
    /// <param name="wallets">List of wallet data for ownership lookup</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple containing the resolved/created principal and whether it's newly created</returns>
    /// <remarks>
    /// This method implements a 4-step resolution process:
    /// 1. Check wallets for existing ownership (if wallets provided)
    /// 2. Fallback to credential-based lookup
    /// 3. Add new credential to existing principal (with conflict detection)
    /// 4. Create new principal if none found
    /// The process is idempotent and safe for retries.
    /// </remarks>
    private async Task<Result<(AxonPrincipal principal, bool isNew), Error>> ResolveOrCreatePrincipalWalletFirst(
        ProviderType providerType,
        string issuer,
        string subject,
        List<ExchangeWalletData> wallets,
        CancellationToken cancellationToken)
    {
        AxonPrincipal? resolvedPrincipal = null;

        // STEP 1: Check wallets first (if provided) - USE BATCH LOOKUP FOR EFFICIENCY
        if (wallets is { Count: > 0 })
        {
            // Parse wallet data and create address pairs for batch lookup (reuse from ProcessWalletsBatch)
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

            // If we have valid wallet specs, batch lookup wallet owners
            if (walletSpecs.Count > 0)
            {
                var walletLookup = await _walletRepository.EnsureManyByChainAndAddressAsync(
                    walletSpecs, cancellationToken);

                // Batch check for existing verified signing ownership (PREFERRED APPROACH)
                var walletOwners = await _principalWriteRepository.FindVerifiedSigningOwnersAsync(
                    walletLookup.Values, cancellationToken);

                if (walletOwners.Count > 0)
                {
                    resolvedPrincipal = walletOwners.First().Value; // Found wallet owner
                }
            }
        }

        // STEP 2: Fallback to credential lookup (existing logic)
        if (resolvedPrincipal is null)
        {
            resolvedPrincipal = await _principalWriteRepository.FindByCredentialAsync(
                providerType, issuer, subject, cancellationToken);
        }

        // STEP 3: Add credential to existing principal (if found)
        if (resolvedPrincipal is not null)
        {
            // Check if credential already exists (idempotency)
            var hasCredential = resolvedPrincipal.Credentials.Any(c =>
                c.Provider == providerType.Value &&
                c.Issuer == issuer &&
                c.Subject == subject);

            if (!hasCredential)
            {
                var credential = IdentityCredential.Create(
                    resolvedPrincipal.Id,
                    providerType.Value,
                    issuer,
                    subject,
                    DateTime.UtcNow);

                // Check credential uniqueness BEFORE calling domain method (proper async pattern)
                var providerTypeResult = ProviderType.Create(credential.Provider);
                if (providerTypeResult.IsFailure)
                    return Result.Failure<(AxonPrincipal, bool), Error>(providerTypeResult.Error);

                var isTaken = await _principalWriteRepository.IsCredentialTakenAsync(
                    providerTypeResult.Value, credential.Issuer, credential.Subject, cancellationToken);

                // Use domain method with synchronous check function
                var addResult = resolvedPrincipal.AddCredential(credential, (provider, iss, subj) =>
                    Result.Success<bool, Error>(isTaken));

                if (addResult.IsFailure)
                {
                    // Convert BusinessRule error to Conflict as specified in Story 3.1 requirements
                    if (addResult.Error.Code == "IDENTITY.CREDENTIAL.BELONGS_TO_OTHER")
                    {
                        return Result.Failure<(AxonPrincipal, bool), Error>(
                            Error.Conflict("This login method belongs to a different account"));
                    }
                    return Result.Failure<(AxonPrincipal, bool), Error>(addResult.Error);
                }
            }

            return (resolvedPrincipal, false); // Existing principal
        }

        // STEP 4: Create new principal (existing CreateWithDynamicCredential logic)
        var createResult = AxonPrincipal.CreateWithDynamicCredential(
            providerType, issuer, subject);

        if (createResult.IsFailure)
            return createResult.Error;

        return (createResult.Value, true); // New principal
    }

    /// <summary>
    /// Processes wallets in batch to prevent N+1 queries and establish verified ownership relationships.
    /// </summary>
    /// <param name="principal">The principal to link wallets to</param>
    /// <param name="wallets">List of wallet data to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing metrics including counts of processed, linked, skipped, and conflicted wallets</returns>
    /// <remarks>
    /// This method implements a 4-step batch processing strategy to prevent N+1 queries:
    /// 1. Parse and validate wallet addresses
    /// 2. Batch ensure wallets exist using repository method
    /// 3. Check for existing verified signing ownership conflicts
    /// 4. Link wallets with verified &amp; signing ownership
    /// Conflicts are detected and reported as domain errors for proper HTTP 409 responses.
    /// </remarks>
    private async Task<Result<WalletProcessingMetrics, Error>> ProcessWalletsBatch(
        AxonPrincipal principal,
        List<ExchangeWalletData> wallets,
        CancellationToken cancellationToken)
    {
        if (wallets is not { Count: > 0 })
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

        // Step 2: Batch ensure wallets exist using wallet repository method
        var walletLookup = await _walletRepository.EnsureManyByChainAndAddressAsync(walletSpecs, cancellationToken);
        var walletIds = walletLookup.Values.ToList();

        // Step 3: Check for existing verified signing ownership conflicts
        var existingOwners = await _principalWriteRepository.FindVerifiedSigningOwnersAsync(walletIds, cancellationToken);
        
        var conflicts = existingOwners.Where(kvp => kvp.Value.Id != principal.Id).ToList();
        if (conflicts.Count > 0)
        {
            // Log wallet ownership conflict with details
            var conflictWallet = conflicts.First();
            var conflictSpec = walletSpecs.First(ws => walletLookup.ContainsKey(ws) && walletLookup[ws] == conflictWallet.Key);

            _logger.LogWarning("Wallet ownership conflict detected: Chain={Chain}, Address={Address}, " +
                "ConflictingPrincipalId={ConflictingPrincipalId}, CurrentPrincipalId={CurrentPrincipalId}",
                conflictSpec.chainId, conflictSpec.address.Value, conflictWallet.Value.Id.Value, principal.Id.Value);

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
    /// Applies chain defaults for verified and signing wallets using optimized batch processing.
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

        // Create chain-to-wallet mappings using deterministic ordering (first wallet per chain by ID)
        var chainWalletMappings = wallets
            .GroupBy(w => w.ChainId)
            .Select(cg => (
                chainId: cg.Key,
                walletId: cg.OrderBy(w => w.Id.Value).First().Id
            ))
            .ToList();

        if (chainWalletMappings.Count == 0)
            return 0;

        _logger.LogDebug("Chain defaults processing: {ChainCount} chains to process using batch method. " +
            "Chains: [{ChainDetails}], ExistingDefaults: {ExistingDefaultsCount}",
            chainWalletMappings.Count,
            string.Join(", ", chainWalletMappings.Select(m => $"{m.chainId}:{m.walletId.Value}")),
            principal.PrincipalChainDefaults.Count);

        // Use optimized batch method that handles all filtering, validation, and no-op detection internally
        var batchResult = principal.ApplyChainDefaultsBatch(chainWalletMappings);

        if (batchResult.IsFailure)
        {
            _logger.LogWarning("Batch chain defaults application failed: {Error}", batchResult.Error.Message);
            return 0;
        }

        _logger.LogDebug("Chain defaults batch processing completed: DefaultsApplied={DefaultsApplied}, " +
            "TotalDefaultsAfter={TotalDefaultsAfter}",
            batchResult.Value, principal.PrincipalChainDefaults.Count);

        return batchResult.Value;
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