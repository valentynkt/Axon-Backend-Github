namespace Axon.Modules.Identity.Domain.Entities;

using Microsoft.AspNetCore.Identity;

/// <summary>
/// Bridge entity that enables Microsoft Identity Framework integration
/// while preserving existing DDD aggregates and domain model.
/// Links to the AxonPrincipal aggregate through AxonUserId.
/// </summary>
public sealed class AxonUserAuth : IdentityUser<Guid>
{
    /// <summary>
    /// Core relationship to domain aggregate (StronglyTypedId of Guid)
    /// Links this Identity user to the AxonPrincipal aggregate
    /// </summary>
    public AxonUserId AxonPrincipalId { get; private set; }

    /// <summary>
    /// Provider information for external authentication
    /// </summary>
    public string ProviderType { get; private set; } = string.Empty;
    public string OriginalIssuer { get; private set; } = string.Empty;
    public string OriginalSubject { get; private set; } = string.Empty;

    /// <summary>
    /// Dynamic.xyz specific fields for integration
    /// </summary>
    public string? DynamicEnvironmentId { get; private set; }
    public string? DynamicUserId { get; private set; }

    /// <summary>
    /// Authentication tracking timestamps
    /// </summary>
    public DateTime? FirstAuthenticatedAt { get; private set; }
    public DateTime LastAuthenticatedAt { get; private set; }

    /// <summary>
    /// Chain and wallet information (denormalized for performance)
    /// </summary>
    public string? PrimaryChainId { get; private set; }
    public string? PrimaryWalletAddress { get; private set; }

    /// <summary>
    /// Disable password-related fields (wallet-based auth only)
    /// We override to return null and ignore set operations
    /// </summary>
    public override string? PasswordHash
    {
        get => null;
        set { /* Ignore - wallet auth only */ }
    }

    /// <summary>
    /// Security stamp for token invalidation
    /// </summary>
    public override string? SecurityStamp { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Private constructor for EF Core
    /// </summary>
    private AxonUserAuth()
    {
        // Required for EF Core
    }

    /// <summary>
    /// Factory method for creating new Identity user from wallet authentication
    /// </summary>
    public static AxonUserAuth Create(
        AxonUserId principalId,
        string providerType,
        string issuer,
        string subject,
        string? dynamicEnvironmentId = null,
        string? dynamicUserId = null)
    {
        var user = new AxonUserAuth
        {
            Id = Guid.CreateVersion7(),
            AxonPrincipalId = principalId,
            UserName = $"{providerType}:{subject}",
            Email = null, // No email required for wallet auth
            ProviderType = providerType,
            OriginalIssuer = issuer,
            OriginalSubject = subject,
            DynamicEnvironmentId = dynamicEnvironmentId,
            DynamicUserId = dynamicUserId,
            FirstAuthenticatedAt = DateTime.UtcNow,
            LastAuthenticatedAt = DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString(),
            EmailConfirmed = true, // Skip email confirmation for wallet auth
            PhoneNumberConfirmed = true, // Skip phone confirmation for wallet auth
            TwoFactorEnabled = false, // Wallet signature is the second factor
            LockoutEnabled = false // No lockout for wallet auth
        };

        // Normalize username for Identity lookups
        user.NormalizedUserName = user.UserName.ToUpperInvariant();

        return user;
    }

    /// <summary>
    /// Update last authenticated timestamp and optionally refresh security stamp
    /// </summary>
    public void UpdateLastAuthenticated(bool refreshSecurityStamp = false)
    {
        LastAuthenticatedAt = DateTime.UtcNow;
        if (refreshSecurityStamp)
        {
            SecurityStamp = Guid.NewGuid().ToString();
        }
    }

    /// <summary>
    /// Set primary chain and wallet information for quick access
    /// </summary>
    public void SetPrimaryWallet(string chainId, string walletAddress)
    {
        PrimaryChainId = chainId;
        PrimaryWalletAddress = walletAddress;
    }

    /// <summary>
    /// Check if user belongs to specific provider
    /// </summary>
    public bool BelongsToProvider(string providerType, string issuer) =>
        ProviderType == providerType &&
        OriginalIssuer == issuer;

    /// <summary>
    /// Check if this is a Dynamic.xyz authenticated user
    /// </summary>
    public bool IsDynamicUser() =>
        ProviderType == "Dynamic" &&
        !string.IsNullOrEmpty(DynamicEnvironmentId);

    /// <summary>
    /// Generate a unique cache key for this user
    /// Used for caching user-specific data
    /// </summary>
    public string GetCacheKey() =>
        $"user:auth:{Id}";

    /// <summary>
    /// Generate a session key for distributed session management
    /// </summary>
    public string GetSessionKey(string sessionId) =>
        $"session:{Id}:{sessionId}";
}