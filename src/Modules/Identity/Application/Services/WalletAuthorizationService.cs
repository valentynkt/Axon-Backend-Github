using System.Security.Claims;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Specifications.AxonPrincipals;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Primitives.Ids;
using Microsoft.AspNetCore.Http;

namespace Axon.Modules.Identity.Application.Services;

/// <summary>
/// Service for determining wallet access permissions and authorization context.
/// Handles tag redaction policy and ownership checks for privacy-compliant queries.
/// </summary>
public interface IWalletAuthorizationService
{
    /// <summary>
    /// Determines if the current user can view wallet tags.
    /// Returns true for wallet owners or service principals with identity:wallets:read-full permission.
    /// </summary>
    Task<bool> CanViewTagsAsync(WalletId walletId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the current user owns the specified wallet.
    /// </summary>
    Task<bool> IsWalletOwnerAsync(WalletId walletId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the current user owns the wallet at the given coordinates.
    /// </summary>
    Task<bool> IsWalletOwnerAsync(ChainId chainId, string rawAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if the current user has the specified service principal permission.
    /// </summary>
    bool HasServicePermission(string permission);

    /// <summary>
    /// Gets the current user's AxonId if authenticated and exists as a principal.
    /// </summary>
    Task<AxonId?> GetCurrentUserAxonIdAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the current user can view the specified principal's data.
    /// Returns true for self-access or service principals with appropriate permissions.
    /// </summary>
    Task<bool> CanViewPrincipalAsync(AxonId targetPrincipalId, CancellationToken cancellationToken = default);
}

public sealed class WalletAuthorizationService : IWalletAuthorizationService
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAxonPrincipalReadRepository _principalRepository;
    private readonly IWalletReadRepository _walletRepository;

    public WalletAuthorizationService(
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor,
        IAxonPrincipalReadRepository principalRepository,
        IWalletReadRepository walletRepository)
    {
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _principalRepository = principalRepository ?? throw new ArgumentNullException(nameof(principalRepository));
        _walletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
    }

    public async Task<bool> CanViewTagsAsync(WalletId walletId, CancellationToken cancellationToken = default)
    {
        // Service principals with full read permission can always view tags
        if (HasServicePermission("identity:wallets:read-full"))
        {
            return true;
        }

        // Check if the current user owns this wallet
        return await IsWalletOwnerAsync(walletId, cancellationToken);
    }

    public async Task<bool> IsWalletOwnerAsync(WalletId walletId, CancellationToken cancellationToken = default)
    {
        var currentAxonId = await GetCurrentUserAxonIdAsync(cancellationToken);
        if (currentAxonId == null)
        {
            return false;
        }

        // Use specification to check ownership
        var spec = new PrincipalByWalletSpec(walletId);
        var principal = await _principalRepository.FirstOrDefaultAsync(spec, cancellationToken);
        
        return principal?.Id == currentAxonId;
    }

    public async Task<bool> IsWalletOwnerAsync(ChainId chainId, string rawAddress, CancellationToken cancellationToken = default)
    {
        var currentAxonId = await GetCurrentUserAxonIdAsync(cancellationToken);
        if (currentAxonId == null)
        {
            return false;
        }

        // Find principals that own wallets on this chain
        var spec = new PrincipalByWalletCoordinatesSpec(chainId);
        var principals = await _principalRepository.ListAsync(spec, cancellationToken);
        
        // Filter by current user and check if any of their wallet ownerships match the address
        var currentUserPrincipal = principals.FirstOrDefault(p => p.Id == currentAxonId);
        if (currentUserPrincipal == null)
        {
            return false;
        }

        // Get wallet IDs for this chain from ownerships
        var walletIds = currentUserPrincipal.WalletOwnerships
            .Where(wo => wo.ChainId == chainId && !wo.IsDeleted)
            .Select(wo => wo.WalletId)
            .ToList();

        if (walletIds.Count == 0)
        {
            return false;
        }

        // Load the wallets and check if any match the address
        var wallets = await _walletRepository.GetByIdsAsync(walletIds, includeDeleted: false, cancellationToken);
        return wallets.Any(w => w.Address.Value.Equals(rawAddress, StringComparison.OrdinalIgnoreCase));
    }

    public bool HasServicePermission(string permission)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        // Check for service principal claims
        var claims = httpContext.User.Claims;
        
        // Look for a permissions claim that contains the required permission
        var permissionsClaim = claims.FirstOrDefault(c => c.Type == "permissions" || c.Type == "scope");
        if (permissionsClaim?.Value != null)
        {
            var permissions = permissionsClaim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return permissions.Contains(permission);
        }

        // Also check for direct permission claim
        return claims.Any(c => c.Type == permission && c.Value.Equals("true", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<AxonId?> GetCurrentUserAxonIdAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || string.IsNullOrEmpty(_currentUserService.UserId))
        {
            return null;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // Extract credential information from claims for Dynamic.xyz integration
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var emailClaim = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;
        var environmentIdClaim = httpContext.User.FindFirst("environment_id")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(emailClaim) || string.IsNullOrEmpty(environmentIdClaim))
        {
            return null;
        }

        // Find principal by Dynamic.xyz credential (provider_type=dynamic, issuer=app.dynamicauth.com/{env}, subject=sub)
        var providerTypeResult = ProviderType.Create("dynamic");
        if (providerTypeResult.IsFailure)
        {
            return null;
        }

        // Format issuer as expected by Dynamic.xyz: app.dynamicauth.com/{env}
        var issuer = $"app.dynamicauth.com/{environmentIdClaim}";
        var spec = new PrincipalByCredentialSpec(providerTypeResult.Value, issuer, userIdClaim);
        var principal = await _principalRepository.FirstOrDefaultAsync(spec, cancellationToken);
        
        return principal?.Id;
    }

    public async Task<bool> CanViewPrincipalAsync(AxonId targetPrincipalId, CancellationToken cancellationToken = default)
    {
        // Check if user has service permission for viewing all principals
        if (HasServicePermission("identity:principals:read-all"))
        {
            return true;
        }

        // Get current user's AxonId
        var currentAxonId = await GetCurrentUserAxonIdAsync(cancellationToken);
        if (currentAxonId == null)
        {
            return false;
        }

        // Allow self-access
        return currentAxonId.Equals(targetPrincipalId);
    }
}