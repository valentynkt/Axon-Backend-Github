using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Application.Queries.GetMyPrincipal;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Application.Observability;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Service responsible for retrieving user profile information including principal data,
/// wallet information, and ETag-based caching support.
/// Separated from authentication concerns following Single Responsibility Principle.
/// </summary>
public sealed class UserProfileService : IUserProfileService
{
    private readonly IAxonPrincipalReadRepository _principalRepository;
    private readonly IWalletReadRepository _walletRepository;
    private readonly ILogger<UserProfileService> _logger;

    public UserProfileService(
        IAxonPrincipalReadRepository principalRepository,
        IWalletReadRepository walletRepository,
        ILogger<UserProfileService> logger)
    {
        _principalRepository = principalRepository ?? throw new ArgumentNullException(nameof(principalRepository));
        _walletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get current user profile by AxonPrincipalId with ETag caching support.
    /// This is the primary method used by authenticated endpoints.
    /// </summary>
    public async Task<Result<CurrentUserResult, Error>> GetCurrentUserProfileAsync(
        AxonUserId principalId,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Find principal by ID
        var principal = await _principalRepository.GetByIdAsync(principalId, cancellationToken);

        if (principal == null)
        {
            return Result.Failure<CurrentUserResult, Error>(
                Error.NotFound(GetMyPrincipalErrorMessages.PrincipalNotFound));
        }

        // Step 2: Generate ETag fingerprint
        var currentETag = await _principalRepository.GetPrincipalFingerprintAsync(
            principal.Id,
            cancellationToken);

        // Step 3: Check If-None-Match header for 304 Not Modified
        var etagResult = CheckETagMatch(ifNoneMatch, currentETag, principal.Id.Value);
        if (etagResult.IsFailure)
        {
            return Result.Failure<CurrentUserResult, Error>(etagResult.Error);
        }

        // Step 4: Load full principal snapshot with ownerships
        var principalWithOwnerships = await _principalRepository.GetByIdWithActiveOwnershipsAsync(
            principal.Id,
            cancellationToken);

        if (principalWithOwnerships == null)
        {
            return Result.Failure<CurrentUserResult, Error>(
                Error.NotFound(GetMyPrincipalErrorMessages.PrincipalDataLoadFailed));
        }

        // Step 5: Build CurrentUserResult response
        var result = await BuildCurrentUserResult(principalWithOwnerships, currentETag, cancellationToken);

        return Result.Success<CurrentUserResult, Error>(result);
    }

    /// <summary>
    /// Legacy method for backward compatibility - gets user profile by provider credentials.
    /// Used when only provider information is available instead of direct principal ID.
    /// </summary>
    public async Task<Result<CurrentUserResult, Error>> GetUserProfileByCredentialAsync(
        ProviderType providerType,
        string issuer,
        string subject,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Find principal by credential
        var principal = await _principalRepository.FindByCredentialAsync(
            providerType,
            issuer,
            subject,
            cancellationToken);

        if (principal == null)
        {
            return Result.Failure<CurrentUserResult, Error>(
                Error.NotFound(GetMyPrincipalErrorMessages.PrincipalNotFound));
        }

        // Delegate to the main method
        return await GetCurrentUserProfileAsync(principal.Id, ifNoneMatch, cancellationToken);
    }

    private Result<bool, Error> CheckETagMatch(string? ifNoneMatch, string currentETag, Guid principalId)
    {
        if (!string.IsNullOrEmpty(ifNoneMatch))
        {
            // Handle both quoted and unquoted ETags as per HTTP spec
            var ifNoneMatchClean = ifNoneMatch.Trim('"');
            if (string.Equals(ifNoneMatchClean, currentETag, StringComparison.OrdinalIgnoreCase))
            {
                // ETag HIT - client cache is still valid
                _logger.LogDebug("ETag HIT: Client ETag {ClientETag} matches current ETag {CurrentETag} for principal {PrincipalId}",
                    ifNoneMatchClean, currentETag, principalId);

                // Record ETag cache hit metric
                Instrumentation.ETagHits.Add(1, new KeyValuePair<string, object?>("endpoint", "/auth/me"));

                return Result.Failure<bool, Error>(
                    Error.Conflict(GetMyPrincipalErrorMessages.ContentNotModified, GetMyPrincipalErrorMessages.NotModifiedCode)
                        .WithMetadata("ETag", currentETag)
                        .WithMetadata("IsNotModified", true));
            }
            else
            {
                // ETag MISS - client cache is stale
                _logger.LogDebug("ETag MISS: Client ETag {ClientETag} does not match current ETag {CurrentETag} for principal {PrincipalId}",
                    ifNoneMatchClean, currentETag, principalId);

                // Record ETag cache miss metric
                Instrumentation.ETagMisses.Add(1, new KeyValuePair<string, object?>("endpoint", "/auth/me"));
            }
        }
        else
        {
            // No If-None-Match header provided - not counted as miss since no cache was attempted
            _logger.LogDebug("No If-None-Match header provided, serving fresh content with ETag {CurrentETag} for principal {PrincipalId}",
                currentETag, principalId);
        }

        return Result.Success<bool, Error>(true);
    }

    private async Task<CurrentUserResult> BuildCurrentUserResult(
        Domain.Aggregates.AxonPrincipal.AxonPrincipal principal,
        string etag,
        CancellationToken cancellationToken)
    {
        // Build user profile per architecture specification
        var profile = new UserProfile(
            AxonId: principal.Id.Value.ToString(),
            RiskTier: CurrentUserResultMapper.MapRiskTierToWire(principal.RiskTier));

        // Build wallets array - only verified wallets for security
        var walletInfos = new List<WalletInfo>();
        var verifiedOwnerships = principal.WalletOwnerships
            .Where(wo => wo.Status == OwnershipStatus.Verified)
            .ToList();

        if (verifiedOwnerships.Count > 0)
        {
            // Get wallet details for verified ownerships
            var walletIds = verifiedOwnerships.Select(wo => wo.WalletId).ToList();
            var wallets = await _walletRepository.GetByIdsAsync(walletIds, false, cancellationToken);

            var walletDict = wallets.ToDictionary(w => w.Id, w => w);

            foreach (var ownership in verifiedOwnerships)
            {
                if (walletDict.TryGetValue(ownership.WalletId, out var wallet))
                {
                    // Check if this wallet is the default for its chain
                    var isDefault = principal.ChainDefaults.TryGetValue(wallet.ChainId.ToString(), out var defaultWalletId)
                                   && defaultWalletId == wallet.Id;

                    walletInfos.Add(new WalletInfo(
                        Chain: wallet.ChainId.ToString(),
                        Address: wallet.Address.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        State: CurrentUserResultMapper.MapOwnershipStatusToWire(ownership.Status),
                        Access: CurrentUserResultMapper.MapAccessModeToWire(ownership.AccessMode),
                        IsDefault: isDefault));
                }
            }
        }

        return new CurrentUserResult(
            Profile: profile,
            Wallets: walletInfos.AsReadOnly(),
            ETag: etag);
    }
}