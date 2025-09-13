using Axon.Modules.Identity.Application.Common.Queries;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Domain.Enums;
using BuildingBlocks.Application.Observability;
using BuildingBlocks.Core.Abstractions.Authentication;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Queries.GetMyPrincipal;

/// <summary>
/// Handler for GetMyPrincipalQuery that resolves authenticated user's principal
/// and provides efficient ETag-based caching support for GET /auth/me endpoint.
/// </summary>
public sealed class GetMyPrincipalHandler : BaseIdentityQueryHandler<GetMyPrincipalQuery, CurrentUserResult>
{
    private readonly IAxonPrincipalReadRepository _principalRepository;
    private readonly IWalletReadRepository _walletRepository;
    private readonly ILogger<GetMyPrincipalHandler> _logger;

    public GetMyPrincipalHandler(
        ICurrentUserService currentUserService,
        IAxonPrincipalReadRepository principalRepository,
        IWalletReadRepository walletRepository,
        ILogger<GetMyPrincipalHandler> logger) : base(currentUserService)
    {
        _principalRepository = principalRepository ?? throw new ArgumentNullException(nameof(principalRepository));
        _walletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<Result<CurrentUserResult, Error>> Handle(
        GetMyPrincipalQuery query,
        CancellationToken cancellationToken)
    {
        // Step 1: Find principal by credential
        var principal = await _principalRepository.FindByCredentialAsync(
            query.ProviderType,
            query.Issuer,
            query.Subject,
            cancellationToken);

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
        if (!string.IsNullOrEmpty(query.IfNoneMatch))
        {
            // Handle both quoted and unquoted ETags as per HTTP spec
            var ifNoneMatch = query.IfNoneMatch.Trim('"');
            if (string.Equals(ifNoneMatch, currentETag, StringComparison.OrdinalIgnoreCase))
            {
                // ETag HIT - client cache is still valid
                _logger.LogDebug("ETag HIT: Client ETag {ClientETag} matches current ETag {CurrentETag} for principal {PrincipalId}", 
                    ifNoneMatch, currentETag, principal.Id.Value);
                
                // Record ETag cache hit metric
                Instrumentation.ETagHits.Add(1, new KeyValuePair<string, object?>("endpoint", "/auth/me"));
                    
                return Result.Failure<CurrentUserResult, Error>(
                    Error.Conflict(GetMyPrincipalErrorMessages.ContentNotModified, GetMyPrincipalErrorMessages.NotModifiedCode)
                        .WithMetadata("ETag", currentETag)
                        .WithMetadata("IsNotModified", true));
            }
            else
            {
                // ETag MISS - client cache is stale
                _logger.LogDebug("ETag MISS: Client ETag {ClientETag} does not match current ETag {CurrentETag} for principal {PrincipalId}", 
                    ifNoneMatch, currentETag, principal.Id.Value);
                
                // Record ETag cache miss metric
                Instrumentation.ETagMisses.Add(1, new KeyValuePair<string, object?>("endpoint", "/auth/me"));
            }
        }
        else
        {
            // No If-None-Match header provided - not counted as miss since no cache was attempted
            _logger.LogDebug("No If-None-Match header provided, serving fresh content with ETag {CurrentETag} for principal {PrincipalId}", 
                currentETag, principal.Id.Value);
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

    private async Task<CurrentUserResult> BuildCurrentUserResult(
        Domain.Aggregates.AxonPrincipal.AxonPrincipal principal, 
        string etag,
        CancellationToken cancellationToken)
    {
        // Build user profile
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
                    walletInfos.Add(new WalletInfo(
                        WalletId: wallet.Id.Value.ToString(),
                        ChainId: wallet.ChainId.ToString(),
                        Address: wallet.Address.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        AccessMode: CurrentUserResultMapper.MapAccessModeToWire(ownership.AccessMode),
                        IsVerified: ownership.Status == OwnershipStatus.Verified));
                }
            }
        }

        // Build chain defaults mapping
        var chainDefaults = principal.ChainDefaults.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Value.ToString());

        return new CurrentUserResult(
            Profile: profile,
            Wallets: walletInfos.AsReadOnly(),
            ChainDefaults: chainDefaults.AsReadOnly(),
            ETag: etag);
    }

}