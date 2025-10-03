namespace Axon.Modules.Identity.Application.Providers;

using System.Security.Claims;
using System.Text.Json;
using System.Diagnostics;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.Contracts.Providers;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Application.Common.Constants;
using global::BuildingBlocks.Core.Utilities;
using global::BuildingBlocks.Core.Diagnostics.Errors;
using global::BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles Dynamic.xyz JWT token exchange with complete business logic.
/// This provider encapsulates all Dynamic-specific authentication and wallet processing.
/// </summary>
public sealed class DynamicAuthenticationProvider : IAuthenticationProvider
{
    private readonly IDynamicAuthService _dynamicAuthService;
    private readonly IAxonPrincipalWriteRepository _principalRepo;
    private readonly IWalletWriteRepository _walletRepo;
    private readonly IPrincipalResolutionService _resolutionService;
    private readonly IAddressNormalizationService _addressNormalizer;
    private readonly IWalletVerificationService _walletVerificationService;
    private readonly IMemoryCache _memoryCache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<AxonUserAuth> _userManager;
    private readonly IIdentityWriteDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DynamicAuthenticationProvider> _logger;

    private static readonly ActivitySource ActivitySource = new("Axon.Identity.DynamicProvider");
    private const string JwtIssuerMetadataKey = "jwt_issuer";

    public DynamicAuthenticationProvider(
        IDynamicAuthService dynamicAuthService,
        IAxonPrincipalWriteRepository principalRepo,
        IWalletWriteRepository walletRepo,
        IPrincipalResolutionService resolutionService,
        IAddressNormalizationService addressNormalizer,
        IWalletVerificationService walletVerificationService,
        IMemoryCache memoryCache,
        IHttpContextAccessor httpContextAccessor,
        UserManager<AxonUserAuth> userManager,
        IIdentityWriteDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<DynamicAuthenticationProvider> logger)
    {
        _dynamicAuthService = dynamicAuthService ?? throw new ArgumentNullException(nameof(dynamicAuthService));
        _principalRepo = principalRepo ?? throw new ArgumentNullException(nameof(principalRepo));
        _walletRepo = walletRepo ?? throw new ArgumentNullException(nameof(walletRepo));
        _resolutionService = resolutionService ?? throw new ArgumentNullException(nameof(resolutionService));
        _addressNormalizer = addressNormalizer ?? throw new ArgumentNullException(nameof(addressNormalizer));
        _walletVerificationService = walletVerificationService ?? throw new ArgumentNullException(nameof(walletVerificationService));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ProviderType => "dynamic";

    public bool CanHandle(AuthenticationRequest request)
    {
        return request is DynamicExchangeRequest;
    }

    public async Task<Result<AuthenticationData, Error>> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is not DynamicExchangeRequest dynamicRequest)
        {
            return Result.Failure<AuthenticationData, Error>(
                Error.Validation("Invalid request type for Dynamic provider"));
        }

        var processingStartTime = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting Dynamic token exchange in provider");

            // Step 1: Validate token and extract user data
            _logger.LogDebug("Provider performing JWT validation (single validation path)");
            var validationResult = await _dynamicAuthService.ValidateTokenAsync(
                dynamicRequest.Token,
                cancellationToken);

            if (validationResult.IsFailure)
            {
                _logger.LogWarning("Dynamic token validation failed: {Error}", validationResult.Error);
                return Result.Failure<AuthenticationData, Error>(validationResult.Error);
            }

            var dynamicUserData = validationResult.Value;

            _logger.LogDebug("JWT validation successful - User: {UserId}, Wallets count: {WalletCount}",
                dynamicUserData.AxonUserId, dynamicUserData.Wallets?.Count ?? 0);

            // Step 2: Normalize wallets and prepare exchange data
            var exchangeWallets = NormalizeWallets(dynamicUserData.Wallets ?? new List<WalletData>());
            _logger.LogDebug("Wallet normalization complete - Normalized count: {NormalizedCount}", exchangeWallets.Count);

            // Extract issuer from token claims
            var rawClaimsResult = await _dynamicAuthService.GetRawClaimsAsync(dynamicRequest.Token, cancellationToken);
            var issuer = rawClaimsResult.IsSuccess
                ? rawClaimsResult.Value.FindFirst("iss")?.Value ?? $"{DynamicAuthConstants.IssuerPrefix}/{dynamicUserData.EnvironmentId}"
                : $"{DynamicAuthConstants.IssuerPrefix}/{dynamicUserData.EnvironmentId}";

            var userData = new ExchangeUserData(
                AxonUserId: dynamicUserData.AxonUserId,
                Email: dynamicUserData.Email,
                DynamicEnvironmentId: dynamicUserData.EnvironmentId,
                Wallets: exchangeWallets,
                FirstVisitUtc: dynamicUserData.FirstVisitUtc,
                LastVisitUtc: dynamicUserData.LastVisitUtc,
                IsNewUser: dynamicUserData.IsNewUser,
                AdditionalMetadata: new Dictionary<string, object> { [JwtIssuerMetadataKey] = issuer }
            );

            // Step 3: Process the exchange with full business logic
            _logger.LogDebug("Starting ProcessExchange for user {UserId} with {WalletCount} wallets",
                userData.AxonUserId, userData.Wallets.Count);

            var exchangeResult = await ProcessExchange(userData, cancellationToken);
            if (exchangeResult.IsFailure)
            {
                _logger.LogError("ProcessExchange failed: {ErrorCode} - {ErrorMessage}",
                    exchangeResult.Error.Code, exchangeResult.Error.Message);
                return Result.Failure<AuthenticationData, Error>(exchangeResult.Error);
            }

            var (principal, isNewPrincipal, metrics) = exchangeResult.Value;
            _logger.LogDebug("ProcessExchange completed - IsNew: {IsNew}, Metrics: Processed={Processed}, Linked={Linked}, Skipped={Skipped}, Conflicts={Conflicts}",
                isNewPrincipal, metrics.Processed, metrics.Linked, metrics.Skipped, metrics.Conflicts);

            // Step 4: Get or create Identity user
            var identityUser = await GetOrCreateIdentityUserAsync(principal, dynamicUserData);
            if (identityUser == null)
            {
                return Result.Failure<AuthenticationData, Error>(
                    Error.Internal("Failed to create Identity user"));
            }

            // Update last authenticated timestamp
            identityUser.UpdateLastAuthenticated();
            await _userManager.UpdateAsync(identityUser);

            // Step 5: Persist changes
            // Note: New principals are already added to DbContext by PrincipalResolutionService.CreateNewPrincipalWithRaceProtectionAsync()
            // Existing principals are already tracked by EF Core from ResolveOrCreatePrincipal() or ProcessWalletsBatch()
            // Changes to tracked entities (credentials, wallets, chain defaults) are automatically detected by EF Core
            // No need to call UpdateAsync - SaveChangesAsync will persist all tracked changes

            // Explicitly commit changes before returning to ensure read queries see latest data
            // This is critical for E2E tests and immediate subsequent /auth/me calls
            // where Read context (separate connection) must see committed writes
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Step 6: Warm caches with all relevant IDs for optimal cache hits
            await WarmUserContextCaches(dynamicUserData.AxonUserId, principal.Id, identityUser.Id);

            processingStartTime.Stop();

            // Build comprehensive additional claims with all metrics
            var additionalClaims = new Dictionary<string, object>
            {
                ["dynamic_user_id"] = dynamicUserData.AxonUserId,
                ["environment_id"] = dynamicUserData.EnvironmentId,
                ["email"] = dynamicUserData.Email ?? string.Empty,
                ["created"] = isNewPrincipal,
                ["wallets_processed"] = metrics.Processed,
                ["wallets_linked"] = metrics.Linked,
                ["defaults_applied"] = metrics.DefaultsApplied,
                ["skipped"] = metrics.Skipped,
                ["conflicts"] = metrics.Conflicts,
                ["processing_time_ms"] = processingStartTime.ElapsedMilliseconds
            };

            var authData = new AuthenticationData(
                User: identityUser,
                ProviderType: "dynamic",
                AdditionalClaims: additionalClaims);

            _logger.LogInformation("Dynamic authentication successful for principal {PrincipalId} in {Duration}ms",
                principal.Id.Value, processingStartTime.ElapsedMilliseconds);

            return Result.Success<AuthenticationData, Error>(authData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CRITICAL: Dynamic authentication failed unexpectedly - Type: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                ex.GetType().FullName, ex.Message, ex.StackTrace);

            // Log inner exception if present
            if (ex.InnerException != null)
            {
                _logger.LogError("Inner Exception - Type: {InnerType}, Message: {InnerMessage}, StackTrace: {InnerStackTrace}",
                    ex.InnerException.GetType().FullName, ex.InnerException.Message, ex.InnerException.StackTrace);
            }

            return Result.Failure<AuthenticationData, Error>(
                Error.Internal($"Dynamic authentication failed: {ex.Message}"));
        }
    }

    private List<ExchangeWalletData> NormalizeWallets(List<WalletData> wallets)
    {
        var exchangeWallets = new List<ExchangeWalletData>();

        foreach (var wallet in wallets)
        {
            var compoundChainId = ChainIdConverter.ConvertToCompoundChainId(wallet.Chain);
            var normalizedAddressResult = _addressNormalizer.NormalizeAddress(compoundChainId, wallet.Address);

            if (normalizedAddressResult.IsFailure)
            {
                _logger.LogWarning("Failed to normalize address {Address} for chain {Chain}: {Error}",
                    wallet.Address, compoundChainId, normalizedAddressResult.Error.Message);
                continue;
            }

            exchangeWallets.Add(new ExchangeWalletData(
                Address: normalizedAddressResult.Value.Value,
                Chain: compoundChainId,
                WalletName: wallet.WalletName,
                Provider: wallet.Provider,
                ConnectedAtUtc: wallet.ConnectedAtUtc
            ));
        }

        return exchangeWallets;
    }

    private async Task<Result<(AxonPrincipal principal, bool isNew, WalletProcessingMetrics metrics), Error>> ProcessExchange(
        ExchangeUserData userData,
        CancellationToken cancellationToken)
    {
        // Step 1: Create credential
        var providerResult = Axon.Modules.Identity.Domain.ValueObjects.ProviderType.Create("dynamic");
        if (providerResult.IsFailure)
            return Result.Failure<(AxonPrincipal, bool, WalletProcessingMetrics), Error>(providerResult.Error);

        var issuer = userData.AdditionalMetadata?.TryGetValue(JwtIssuerMetadataKey, out var jwtIssuer) == true && jwtIssuer is string jwtIssuerStr
            ? jwtIssuerStr
            : $"{DynamicAuthConstants.IssuerPrefix}/{userData.DynamicEnvironmentId}";

        // Step 2: Resolve or create principal
        var principalResult = await ResolveOrCreatePrincipal(
            providerResult.Value, issuer, userData.AxonUserId, userData.Wallets, cancellationToken);
        if (principalResult.IsFailure)
            return Result.Failure<(AxonPrincipal, bool, WalletProcessingMetrics), Error>(principalResult.Error);

        var (principal, isNewPrincipal) = principalResult.Value;

        // Step 3: Process wallets in batch
        var walletProcessingResult = await ProcessWalletsBatch(principal, userData.Wallets, cancellationToken);
        if (walletProcessingResult.IsFailure)
            return Result.Failure<(AxonPrincipal, bool, WalletProcessingMetrics), Error>(walletProcessingResult.Error);

        var walletMetrics = walletProcessingResult.Value;

        // Step 4: Apply chain defaults
        var defaultsApplied = await ApplyChainDefaults(principal, walletMetrics.ProcessedWalletIds, cancellationToken);
        walletMetrics = walletMetrics with { DefaultsApplied = defaultsApplied };

        return Result.Success<(AxonPrincipal, bool, WalletProcessingMetrics), Error>((principal, isNewPrincipal, walletMetrics));
    }

    private async Task<Result<(AxonPrincipal principal, bool isNew), Error>> ResolveOrCreatePrincipal(
        ProviderType providerType,
        string issuer,
        string subject,
        List<ExchangeWalletData> wallets,
        CancellationToken cancellationToken)
    {
        if (wallets is not { Count: > 0 })
        {
            var principal = await _principalRepo.FindByCredentialAsync(providerType, issuer, subject, cancellationToken);
            if (principal is not null)
                return (principal, false);

            var createResult = AxonPrincipal.CreateWithDynamicCredential(providerType, issuer, subject);
            if (createResult.IsFailure)
                return createResult.Error;

            var newPrincipal = createResult.Value;

            // Add to DbContext so it's tracked for SaveChanges by UnitOfWorkBehavior
            // This handles the no-wallets scenario where PrincipalResolutionService is not called
            await _principalRepo.AddAsync(newPrincipal, cancellationToken);

            return (newPrincipal, true);
        }

        var firstWallet = wallets[0];
        var normalizedAddressResult = _addressNormalizer.NormalizeAddress(firstWallet.Chain, firstWallet.Address);
        if (normalizedAddressResult.IsFailure)
            return normalizedAddressResult.Error;

        var chainIdResult = ChainId.Create(firstWallet.Chain);
        if (chainIdResult.IsFailure)
            return chainIdResult.Error;

        using var activity = ActivitySource.StartActivity("DynamicProvider.PrincipalResolution");

        var resolutionResult = await _resolutionService.ResolveAsync(
            providerType, issuer, subject,
            chainIdResult.Value, normalizedAddressResult.Value, cancellationToken);

        if (resolutionResult.IsFailure)
            return resolutionResult.Error;

        var result = resolutionResult.Value;
        var isNewPrincipal = result.Path == ResolutionPath.Created;

        _logger.LogInformation("Principal resolution completed: principal_id={PrincipalId} resolution_path={ResolutionPath}",
            result.Principal.Id.Value, result.Path);

        if (!isNewPrincipal)
        {
            await HandleCredentialManagement(result.Principal, providerType, issuer, subject, cancellationToken);
        }

        return (result.Principal, isNewPrincipal);
    }

    private async Task HandleCredentialManagement(
        AxonPrincipal principal,
        ProviderType providerType,
        string issuer,
        string subject,
        CancellationToken cancellationToken)
    {
        var existingCredential = principal.Credentials.FirstOrDefault(c =>
            c.Provider == providerType.Value &&
            c.Issuer == issuer &&
            c.Subject == subject);

        if (existingCredential is not null)
        {
            existingCredential.UpdateLastSeen(DateTime.UtcNow);
            return;
        }

        var credential = IdentityCredential.Create(
            principal.Id,
            providerType.Value,
            issuer,
            subject,
            DateTime.UtcNow);

        // Check if credential is taken by ANOTHER principal (exclude current principal from check)
        var isTaken = await _principalRepo.IsCredentialTakenAsync(
            providerType, credential.Issuer, credential.Subject, principal.Id, cancellationToken);

        var addResult = principal.AddCredential(credential, (provider, iss, subj) =>
            Result.Success<bool, Error>(isTaken), _timeProvider);

        if (addResult.IsFailure)
        {
            if (addResult.Error.Code == "IDENTITY.CREDENTIAL.BELONGS_TO_OTHER")
            {
                throw new InvalidOperationException("This login method belongs to a different account");
            }
            throw new InvalidOperationException(addResult.Error.Message);
        }
    }

    private async Task<Result<WalletProcessingMetrics, Error>> ProcessWalletsBatch(
        AxonPrincipal principal,
        List<ExchangeWalletData> wallets,
        CancellationToken cancellationToken)
    {
        if (wallets is not { Count: > 0 })
        {
            return Result.Success<WalletProcessingMetrics, Error>(
                new WalletProcessingMetrics(0, 0, 0, 0, 0, new List<WalletId>()));
        }

        var walletSpecs = new List<(string chainId, Address address)>();

        foreach (var wallet in wallets)
        {
            var chainIdResult = ChainId.Create(wallet.Chain);
            if (chainIdResult.IsFailure)
                continue;

            var normalizedAddressResult = _addressNormalizer.NormalizeAddress(wallet.Chain, wallet.Address);
            if (normalizedAddressResult.IsFailure)
                continue;

            walletSpecs.Add((chainIdResult.Value.Value, normalizedAddressResult.Value));
        }

        var walletLookup = await _walletRepo.EnsureManyByChainAndAddressAsync(walletSpecs, cancellationToken);
        var walletIds = walletLookup.Values.ToList();

        var linked = 0;
        var skipped = 0;
        var conflicts = 0;

        foreach (var walletId in walletIds)
        {
            var verificationResult = await _walletVerificationService.VerifyWalletOwnershipAsync(
                walletId,
                principal.Id,
                AccessMode.Signing,
                VerificationSource.DynamicAttested,
                cancellationToken);

            if (verificationResult.IsFailure)
            {
                if (verificationResult.Error.Code.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase))
                {
                    conflicts++;
                    var conflictSpec = walletSpecs.First(ws => walletLookup.ContainsKey(ws) && walletLookup[ws] == walletId);
                    return Result.Failure<WalletProcessingMetrics, Error>(
                        IdentityDomainErrors.Wallet.WalletOwnershipConflict(conflictSpec.chainId, conflictSpec.address.Value));
                }
                else
                {
                    skipped++;
                }
            }
            else
            {
                var ownership = verificationResult.Value;
                var linkResult = principal.LinkWalletOwnership(ownership, (wId, mode, status) =>
                    Result.Success<bool, Error>(false), _timeProvider);

                if (linkResult.IsSuccess)
                    linked++;
                else
                    skipped++;
            }
        }

        return Result.Success<WalletProcessingMetrics, Error>(
            new WalletProcessingMetrics(wallets.Count, linked, skipped, conflicts, 0, walletIds));
    }

    private async Task<int> ApplyChainDefaults(
        AxonPrincipal principal,
        List<WalletId> processedWalletIds,
        CancellationToken cancellationToken)
    {
        var eligibleOwnerships = principal.WalletOwnerships
            .Where(wo => processedWalletIds.Contains(wo.WalletId) &&
                        wo.Status == OwnershipStatus.Verified &&
                        wo.AccessMode == AccessMode.Signing)
            .ToList();

        if (eligibleOwnerships.Count == 0)
            return 0;

        var wallets = await _walletRepo.GetByIdsAsync(
            eligibleOwnerships.Select(o => o.WalletId),
            cancellationToken);

        var chainWalletMappings = wallets
            .GroupBy(w => w.ChainId)
            .Select(cg => (
                chainId: cg.Key,
                walletId: cg.OrderBy(w => w.Id.Value).First().Id
            ))
            .ToList();

        if (chainWalletMappings.Count == 0)
            return 0;

        var batchResult = principal.ApplyChainDefaultsBatch(chainWalletMappings, _timeProvider);
        return batchResult.IsSuccess ? batchResult.Value : 0;
    }

    private async Task WarmUserContextCaches(string dynamicUserId, AxonUserId axonUserId, Guid identityUserId)
    {
        try
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(15),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                Priority = CacheItemPriority.High
            };

            // Warm cache with Dynamic user ID (for backward compatibility)
            var dynamicCacheKey = $"axon:user:{dynamicUserId}";
            _memoryCache.Set(dynamicCacheKey, axonUserId, cacheOptions);
            _logger.LogDebug("Cache warmed with Dynamic key: {CacheKey} -> {AxonUserId}",
                dynamicCacheKey, axonUserId.Value);

            // Warm cache with Identity user ID (what JWT NameIdentifier will contain)
            // This is the PRIMARY cache key that HttpContextUserService will use
            var identityCacheKey = $"axon:user:{identityUserId}";
            _memoryCache.Set(identityCacheKey, axonUserId, cacheOptions);
            _logger.LogDebug("Cache warmed with Identity key: {CacheKey} -> {AxonUserId}",
                identityCacheKey, axonUserId.Value);

            // Also warm cache with the AxonUserId GUID (for completeness)
            var axonUserCacheKey = $"axon:user:{axonUserId.Value}";
            _memoryCache.Set(axonUserCacheKey, axonUserId, cacheOptions);
            _logger.LogDebug("Cache warmed with Axon key: {CacheKey} -> {AxonUserId}",
                axonUserCacheKey, axonUserId.Value);

            // Set in HttpContext.Items for request-scoped caching
            if (_httpContextAccessor.HttpContext != null)
            {
                _httpContextAccessor.HttpContext.Items["AxonUserId"] = axonUserId;
                _logger.LogDebug("Request cache warmed in HttpContext.Items");
            }

            _logger.LogInformation("User context caches fully warmed: Dynamic:{DynamicUserId}, Identity:{IdentityUserId} -> Axon:{AxonUserId}",
                dynamicUserId, identityUserId, axonUserId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache warming failed for user Dynamic:{DynamicUserId}, Identity:{IdentityUserId}, Axon:{AxonUserId}",
                dynamicUserId, identityUserId, axonUserId.Value);
        }

        await Task.CompletedTask;
    }

    private async Task<AxonUserAuth?> GetOrCreateIdentityUserAsync(
        AxonPrincipal principal,
        DynamicUserData dynamicUserData)
    {
        try
        {
            var existingUsers = await _userManager.GetUsersForClaimAsync(
                new Claim("dynamic_user_id", dynamicUserData.AxonUserId));

            var existingUser = existingUsers.FirstOrDefault();

            if (existingUser == null)
            {
                existingUsers = await _userManager.GetUsersForClaimAsync(
                    new Claim("axon_principal_id", principal.Id.Value.ToString()));
                existingUser = existingUsers.FirstOrDefault();
            }

            if (existingUser != null)
            {
                existingUser.UpdateLastAuthenticated();
                await _userManager.UpdateAsync(existingUser);
                return existingUser;
            }

            var newUser = AxonUserAuth.Create(
                principalId: principal.Id,
                providerType: "dynamic",
                issuer: "https://app.dynamic.xyz",
                subject: dynamicUserData.AxonUserId,
                dynamicEnvironmentId: dynamicUserData.EnvironmentId,
                dynamicUserId: dynamicUserData.AxonUserId);

            if (!string.IsNullOrEmpty(dynamicUserData.Email))
            {
                newUser.Email = dynamicUserData.Email;
                newUser.NormalizedEmail = dynamicUserData.Email.ToUpperInvariant();
            }

            var createResult = await _userManager.CreateAsync(newUser);

            if (!createResult.Succeeded)
            {
                // Check if failure is due to duplicate username (race condition with concurrent request)
                var isDuplicateUsername = createResult.Errors.Any(e =>
                    e.Code == "DuplicateUserName" || e.Description.Contains("already taken", StringComparison.OrdinalIgnoreCase));

                if (isDuplicateUsername)
                {
                    // Retry fetching by username - the user was likely just created by a concurrent request
                    // We search by username (not claims) because claims might not be persisted yet
                    // Add brief delay to allow transaction commit from concurrent request
                    _logger.LogWarning("Duplicate username detected (race condition). Retrying user fetch by username for Dynamic user {DynamicUserId}",
                        dynamicUserData.AxonUserId);

                    // Retry with exponential backoff (up to 3 attempts)
                    for (int attempt = 1; attempt <= 3; attempt++)
                    {
                        await Task.Delay(attempt * 50); // 50ms, 100ms, 150ms

                        existingUser = await _userManager.FindByNameAsync(newUser.UserName!);

                        if (existingUser != null)
                        {
                            _logger.LogInformation("Successfully retrieved user after race condition on attempt {Attempt} for Dynamic user {DynamicUserId}",
                                attempt, dynamicUserData.AxonUserId);
                            return existingUser;
                        }
                    }

                    _logger.LogWarning("Failed to retrieve user after {MaxAttempts} retry attempts for Dynamic user {DynamicUserId}",
                        3, dynamicUserData.AxonUserId);
                }

                _logger.LogError("Failed to create Identity user: {Errors}",
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return null;
            }

            await _userManager.AddClaimAsync(newUser,
                new Claim("axon_principal_id", principal.Id.Value.ToString()));
            await _userManager.AddClaimAsync(newUser,
                new Claim("dynamic_user_id", dynamicUserData.AxonUserId));
            await _userManager.AddClaimAsync(newUser,
                new Claim("dynamic_environment_id", dynamicUserData.EnvironmentId));

            _logger.LogInformation("Created new Identity user {UserId} for Dynamic user {DynamicUserId}",
                newUser.Id, dynamicUserData.AxonUserId);

            return newUser;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get or create Identity user");
            return null;
        }
    }

    private sealed record WalletProcessingMetrics(
        int Processed,
        int Linked,
        int Skipped,
        int Conflicts,
        int DefaultsApplied,
        List<WalletId> ProcessedWalletIds);
}