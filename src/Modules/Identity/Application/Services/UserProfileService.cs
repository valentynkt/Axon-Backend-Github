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
/// Service responsible for retrieving user profile information including principal data
/// and wallet information.
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
    /// Get current user profile by AxonPrincipalId.
    /// This is the primary method used by authenticated endpoints.
    /// </summary>
    public async Task<Result<CurrentUserResult, Error>> GetCurrentUserProfileAsync(
        AxonUserId principalId,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Find principal by ID
        var principal = await _principalRepository.GetByIdAsync(principalId, cancellationToken);

        if (principal == null)
        {
            return Result.Failure<CurrentUserResult, Error>(
                Error.NotFound(GetMyPrincipalErrorMessages.PrincipalNotFound));
        }

        // Step 2: Load full principal snapshot with ownerships
        var principalWithOwnerships = await _principalRepository.GetByIdWithActiveOwnershipsAsync(
            principal.Id,
            cancellationToken);

        if (principalWithOwnerships == null)
        {
            return Result.Failure<CurrentUserResult, Error>(
                Error.NotFound(GetMyPrincipalErrorMessages.PrincipalDataLoadFailed));
        }

        // Step 3: Build CurrentUserResult response
        var result = await BuildCurrentUserResult(principalWithOwnerships, cancellationToken);

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
        return await GetCurrentUserProfileAsync(principal.Id, cancellationToken);
    }

    private async Task<CurrentUserResult> BuildCurrentUserResult(
        Domain.Aggregates.AxonPrincipal.AxonPrincipal principal,
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

            // Build chain defaults dictionary from the collection (not the computed property)
            // to avoid EF Core issues with owned entity types
            // Must force complete materialization to avoid EF Core query translation issues
            // IMPORTANT: Normalize chainId to lowercase for case-insensitive comparison
            var activeDefaults = new List<(string chainId, WalletId walletId)>();
            foreach (var pcd in principal.PrincipalChainDefaults)
            {
                if (!pcd.IsDeleted)
                {
                    activeDefaults.Add((pcd.ChainId.ToLowerInvariant(), pcd.WalletId));
                }
            }
            var chainDefaultsDict = activeDefaults.ToDictionary(x => x.chainId, x => x.walletId);

            foreach (var ownership in verifiedOwnerships)
            {
                if (walletDict.TryGetValue(ownership.WalletId, out var wallet))
                {
                    // Check if this wallet is the default for its chain
                    var isDefault = chainDefaultsDict.TryGetValue(wallet.ChainId.ToLowerInvariant(), out var defaultWalletId)
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
            Wallets: walletInfos.AsReadOnly());
    }
}