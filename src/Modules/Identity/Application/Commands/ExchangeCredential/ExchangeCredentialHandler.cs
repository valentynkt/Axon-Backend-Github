using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.Common.Constants;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.DTOs.Exchange;
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
    private static readonly ActivitySource ActivitySource = new("Axon.Identity.ExchangeCredential");
    private const string JwtIssuerMetadataKey = "jwt_issuer";
    private readonly IAxonPrincipalWriteRepository _principalWriteRepository;
    private readonly IWalletWriteRepository _walletRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPrincipalResolutionService _resolutionService;
    private readonly IAddressNormalizationService _addressNormalizer;
    private readonly IWalletVerificationService _walletVerificationService;
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
        IMemoryCache memoryCache,
        IHttpContextAccessor httpContextAccessor,
        IPrincipalResolutionService resolutionService,
        IAddressNormalizationService addressNormalizer,
        IWalletVerificationService walletVerificationService,
        ILogger<ExchangeCredentialHandler> logger)
        : base(currentUserService)
    {
        _principalWriteRepository = principalRepository;
        _walletRepository = walletRepository;
        _memoryCache = memoryCache;
        _httpContextAccessor = httpContextAccessor;
        _resolutionService = resolutionService;
        _addressNormalizer = addressNormalizer;
        _walletVerificationService = walletVerificationService;
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
            ["AxonUserId"] = command.UserData?.AxonUserId ?? "unknown",
            ["DynamicEnvironmentId"] = command.UserData?.DynamicEnvironmentId ?? "unknown",
            ["WalletCount"] = command.UserData?.Wallets?.Count ?? 0
        });

        _logger.LogInformation("Exchange credential operation started for user {AxonUserId} with {WalletCount} wallets",
            command.UserData?.AxonUserId, command.UserData?.Wallets?.Count ?? 0);

        // Validate input data structure
        var validationResult = await ValidateCommand(command);
        if (validationResult.IsFailure)
        {
            processingStartTime.Stop();
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
                _logger.LogInformation("Exchange completed successfully: Created={Created}, WalletsProcessed={WalletsProcessed}, " +
                    "WalletsLinked={WalletsLinked}, Conflicts={Conflicts}, DefaultsApplied={DefaultsApplied}, Duration={Duration}ms",
                    outcome.Created, outcome.WalletsProcessed, outcome.WalletsLinked,
                    outcome.Conflicts, outcome.DefaultsApplied, processingStartTime.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogWarning("Exchange failed: {ErrorCode} - {Message}",
                    result.Error.Code, result.Error.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            processingStartTime.Stop();
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
    /// <param name="dynamicAxonUserId">The Dynamic user ID from JWT token</param>
    /// <param name="axonAxonUserId">The resolved internal AxonUserId</param>
    /// <returns>Task representing the async cache warming operation</returns>
    private async Task WarmUserContextCaches(string dynamicAxonUserId, AxonUserId axonAxonUserId)
    {
        using var activity = Activity.Current?.Source.StartActivity("WarmUserContextCaches");
        activity?.SetTag("dynamic_user_id", dynamicAxonUserId);
        activity?.SetTag("axon_user_id", axonAxonUserId.Value);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Warm memory cache for cross-request access
            var cacheKey = $"axon:user:{dynamicAxonUserId}";
            _memoryCache.Set(cacheKey, axonAxonUserId, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(15),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                Priority = CacheItemPriority.High
            });

            // Warm request-scoped cache for immediate use
            if (_httpContextAccessor.HttpContext != null)
            {
                _httpContextAccessor.HttpContext.Items["AxonUserId"] = axonAxonUserId;
            }

            stopwatch.Stop();

            // Record success metrics
            CacheWarmingSuccessCounter.Add(1);
            CacheWarmingLatency.Record(stopwatch.ElapsedMilliseconds);

            _logger.LogInformation("User context cache warmed: {DynamicAxonUserId} -> {AxonUserId}, Duration: {Duration}ms",
                dynamicAxonUserId, axonAxonUserId.Value, stopwatch.ElapsedMilliseconds);
                
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
            _logger.LogWarning(ex, "Cache warming failed for user {DynamicAxonUserId}, Duration: {Duration}ms", 
                dynamicAxonUserId, stopwatch.ElapsedMilliseconds);
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

        // Step 2: Resolve or create Principal using new resolution service
        // ChainIds are now compound format (e.g., "solana-mainnet") containing all network information
        var principalResult = await ResolveOrCreatePrincipalWithNewService(
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

        // Step 5: Apply verified-first chain defaults
        var defaultsApplied = await ApplyChainDefaults(principal, walletMetrics.ProcessedWalletIds, cancellationToken);

        _logger.LogDebug("Before persistence: PrincipalId={PrincipalId}, DefaultsApplied={DefaultsApplied}, " +
            "ChainDefaultsCount={ChainDefaultsCount}, WalletOwnershipsCount={WalletOwnershipsCount}",
            principal.Id.Value, defaultsApplied, principal.PrincipalChainDefaults.Count, principal.WalletOwnerships.Count);

        // Step 6: Persist changes
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

        // Step 7: Warm user context caches for subsequent identity resolution
        await WarmUserContextCaches(userData.AxonUserId, principal.Id);

        // Step 8: Return stable metrics
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
    /// Validates that UserData, AxonUserId, and EnvironmentId are provided and non-empty.
    /// This method performs structural validation only - business rule validation occurs later in the flow.
    /// </remarks>
    private static async Task<Result<ExchangeOutcome, Error>> ValidateCommand(ExchangeCredentialCommand command)
    {
        if (command.UserData is not { } userData)
            return Result.Failure<ExchangeOutcome, Error>(
                Error.Validation("User data is required", "EXCHANGE.USER_DATA_REQUIRED"));

        if (string.IsNullOrWhiteSpace(userData.AxonUserId))
            return Result.Failure<ExchangeOutcome, Error>(
                Error.Validation("User ID is required", "EXCHANGE.USER_ID_REQUIRED"));

        if (string.IsNullOrWhiteSpace(userData.DynamicEnvironmentId))
            return Result.Failure<ExchangeOutcome, Error>(
                Error.Validation("Dynamic Environment ID is required", "EXCHANGE.ENV_ID_REQUIRED"));

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
            : $"{DynamicAuthConstants.IssuerPrefix}/{userData.DynamicEnvironmentId}";

        // Log which issuer source was used for debugging
        var issuerSource = userData.AdditionalMetadata?.ContainsKey(JwtIssuerMetadataKey) == true ? "JWT claim" : "constructed";
        System.Diagnostics.Debug.WriteLine($"Using issuer from {issuerSource}: {issuer}");
        var subject = userData.AxonUserId;

        return Result.Success<(ProviderType, string, string), Error>((providerResult.Value, issuer, subject));
    }

    /// <summary>
    /// Resolves or creates a principal using the new PrincipalResolutionService.
    /// Implements deterministic 2-step resolution with cross-network conflict detection.
    /// </summary>
    /// <param name="providerType">Authentication provider type</param>
    /// <param name="issuer">Token issuer identifier</param>
    /// <param name="subject">Token subject identifier</param>
    /// <param name="wallets">List of wallet data for resolution (ChainIds must be in compound format)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple containing the resolved/created principal and whether it's newly created</returns>
    private async Task<Result<(AxonPrincipal principal, bool isNew), Error>> ResolveOrCreatePrincipalWithNewService(
        ProviderType providerType,
        string issuer,
        string subject,
        List<ExchangeWalletData> wallets,
        CancellationToken cancellationToken)
    {
        // For now, use the first wallet for resolution. In the future, we might want to handle multiple wallets
        if (wallets is not { Count: > 0 })
        {
            // If no wallets, fall back to credential-only lookup
            var principal = await _principalWriteRepository.FindByCredentialAsync(
                providerType, issuer, subject, cancellationToken);

            if (principal is not null)
            {
                return (principal, false);
            }

            // Create new principal if none found
            var createResult = AxonPrincipal.CreateWithDynamicCredential(providerType, issuer, subject);
            if (createResult.IsFailure)
                return createResult.Error;

            return (createResult.Value, true);
        }

        // Use the first wallet for resolution
        var firstWallet = wallets[0];

        // Normalize the address first
        var normalizedAddressResult = _addressNormalizer.NormalizeAddress(firstWallet.Chain, firstWallet.Address);
        if (normalizedAddressResult.IsFailure)
            return normalizedAddressResult.Error;

        var chainIdResult = ChainId.Create(firstWallet.Chain);
        if (chainIdResult.IsFailure)
            return chainIdResult.Error;

        // Use the PrincipalResolutionService for deterministic resolution
        using var activity = ActivitySource.StartActivity("ExchangeCredential.PrincipalResolution");
        activity?.SetTag("provider", providerType.Value);
        activity?.SetTag("chain_id", chainIdResult.Value.Value);

        _logger.LogInformation(
            "Delegating principal resolution to PrincipalResolutionService: " +
            "provider={Provider} chain_id={ChainId} address={Address}",
            providerType.Value, chainIdResult.Value.Value, normalizedAddressResult.Value.Value);

        var resolutionResult = await _resolutionService.ResolveAsync(
            providerType, issuer, subject,
            chainIdResult.Value, normalizedAddressResult.Value, cancellationToken);

        if (resolutionResult.IsFailure)
            return resolutionResult.Error;

        var result = resolutionResult.Value;
        var isNewPrincipal = result.Path == ResolutionPath.Created;

        activity?.SetTag("resolution_path", result.Path.ToString());
        activity?.SetTag("was_auto_linked", result.WasAutoLinked);
        activity?.SetTag("principal_id", result.Principal.Id.Value.ToString());

        _logger.LogInformation(
            "Principal resolution completed: principal_id={PrincipalId} resolution_path={ResolutionPath} " +
            "was_auto_linked={WasAutoLinked} provider={Provider}",
            result.Principal.Id.Value, result.Path, result.WasAutoLinked, providerType.Value);

        // Handle credential management: update last_seen_at or add new credential
        if (!isNewPrincipal)
        {
            await HandleCredentialManagement(result.Principal, providerType, issuer, subject, cancellationToken);
        }

        return (result.Principal, isNewPrincipal);
    }

    /// <summary>
    /// Handles credential management for existing principals.
    /// Implements monotonic last_seen_at updates and adds new credentials if needed.
    /// </summary>
    /// <param name="principal">The existing principal</param>
    /// <param name="providerType">Provider type</param>
    /// <param name="issuer">Token issuer</param>
    /// <param name="subject">Token subject</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task HandleCredentialManagement(
        AxonPrincipal principal,
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken cancellationToken)
    {
        // Check if credential already exists
        var existingCredential = principal.Credentials.FirstOrDefault(c =>
            c.Provider == providerType.Value &&
            c.Issuer == issuer &&
            c.Subject == subject);

        if (existingCredential is not null)
        {
            // Update last_seen_at monotonically (only if newer)
            var now = DateTime.UtcNow;
            existingCredential.UpdateLastSeen(now);

            _logger.LogDebug(
                "Updated credential last_seen_at for {Provider}:{Issuer}:{Subject} to {LastSeenAt}",
                providerType.Value, issuer, subject, now);
            return;
        }

        // Create new credential if it doesn't exist
        var credential = IdentityCredential.Create(
            principal.Id,
            providerType.Value,
            issuer,
            subject,
            DateTime.UtcNow);

        // Check credential uniqueness BEFORE calling domain method
        var isTaken = await _principalWriteRepository.IsCredentialTakenAsync(
            providerType, credential.Issuer, credential.Subject, cancellationToken);

        // Use domain method with synchronous check function
        var addResult = principal.AddCredential(credential, (provider, iss, subj) =>
            Result.Success<bool, Error>(isTaken));

        if (addResult.IsFailure)
        {
            // Convert BusinessRule error to Conflict as specified in requirements
            if (addResult.Error.Code == "IDENTITY.CREDENTIAL.BELONGS_TO_OTHER")
            {
                _logger.LogWarning(
                    "Credential conflict: {Provider}:{Issuer}:{Subject} belongs to different account",
                    providerType.Value, issuer, subject);
                throw new InvalidOperationException("This login method belongs to a different account");
            }
            throw new InvalidOperationException(addResult.Error.Message);
        }

        _logger.LogDebug(
            "Added new credential {Provider}:{Issuer}:{Subject} to principal {PrincipalId}",
            providerType.Value, issuer, subject, principal.Id.Value);
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

        // Step 1: Parse wallet data and create address pairs for batch lookup with normalization
        var walletSpecs = new List<(string chainId, Address address)>();
        var parseErrors = new List<string>();

        foreach (var wallet in wallets)
        {
            // Validate chain ID first
            var chainIdResult = ChainId.Create(wallet.Chain);
            if (chainIdResult.IsFailure)
            {
                parseErrors.Add($"Invalid chain {wallet.Chain}: {chainIdResult.Error.Message}");
                continue;
            }

            // Normalize address using the normalization service
            var normalizedAddressResult = _addressNormalizer.NormalizeAddress(
                wallet.Chain, wallet.Address);
            if (normalizedAddressResult.IsFailure)
            {
                parseErrors.Add($"Invalid address {wallet.Address}: {normalizedAddressResult.Error.Message}");
                continue;
            }

            walletSpecs.Add((wallet.Chain, normalizedAddressResult.Value));
        }

        _logger.LogDebug(
            "Processed {WalletCount} wallets",
            walletSpecs.Count);

        if (parseErrors.Count > 0)
        {
            return Result.Failure<WalletProcessingMetrics, Error>(
                Error.Validation($"Address parsing errors: {string.Join(", ", parseErrors)}", "EXCHANGE.INVALID_ADDRESSES"));
        }

        // Step 2: Batch ensure wallets exist using wallet repository method
        var walletLookup = await _walletRepository.EnsureManyByChainAndAddressAsync(walletSpecs, cancellationToken);
        var walletIds = walletLookup.Values.ToList();

        // Step 3 & 4: Verify wallet ownerships using the new verification service with transaction guards
        var linked = 0;
        var skipped = 0;
        var conflicts = 0;

        foreach (var walletId in walletIds)
        {
            // Use the new WalletVerificationService which handles:
            // - Transaction scoping with row-level locks
            // - Checking for existing verified signing ownership conflicts
            // - Auto-revoking pending ownerships
            // - Retry logic for race conditions
            var verificationResult = await _walletVerificationService.VerifyWalletOwnershipAsync(
                walletId,
                principal.Id,
                Domain.Enums.AccessMode.Signing,
                Domain.Enums.VerificationSource.DynamicAttested,
                cancellationToken);

            if (verificationResult.IsFailure)
            {
                // Check if it's a conflict error (409)
                if (verificationResult.Error.Code.Contains("ALREADY_VERIFIED", StringComparison.OrdinalIgnoreCase) ||
                    verificationResult.Error.Code.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase))
                {
                    conflicts++;

                    // Get wallet details for logging
                    var conflictSpec = walletSpecs.First(ws => walletLookup.ContainsKey(ws) && walletLookup[ws] == walletId);

                    _logger.LogWarning("Wallet ownership conflict detected: Chain={Chain}, Address={Address}, " +
                        "CurrentPrincipalId={CurrentPrincipalId}, Error={Error}",
                        conflictSpec.chainId, conflictSpec.address.Value, principal.Id.Value, verificationResult.Error.Message);

                    return Result.Failure<WalletProcessingMetrics, Error>(
                        IdentityDomainErrors.Wallet.WalletOwnershipConflict(conflictSpec.chainId, conflictSpec.address.Value));
                }
                else
                {
                    // Other errors - skip this wallet
                    skipped++;
                    _logger.LogWarning("Failed to verify wallet ownership for WalletId={WalletId}: {Error}",
                        walletId, verificationResult.Error.Message);
                }
            }
            else
            {
                // Successfully verified - link to principal
                var ownership = verificationResult.Value;

                // Check function for LinkWalletOwnership (should always succeed since we already verified)
                var checkExistingOwnership = (WalletId wId, Domain.Enums.AccessMode mode, Domain.Enums.OwnershipStatus status) =>
                {
                    // Already verified by service, no conflicts
                    return Result.Success<bool, Error>(false);
                };

                var linkResult = principal.LinkWalletOwnership(ownership, checkExistingOwnership);
                if (linkResult.IsSuccess)
                {
                    linked++;
                }
                else
                {
                    skipped++;
                    _logger.LogWarning("Failed to link verified ownership to principal: {Error}",
                        linkResult.Error.Message);
                }
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



        _logger.LogDebug("Chain defaults processing: {ChainCount} chains to process using batch method for network. " +
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