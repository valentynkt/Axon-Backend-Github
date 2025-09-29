using System.ComponentModel.DataAnnotations;

namespace Axon.Modules.Identity.Infrastructure.Services.Configuration;

/// <summary>
/// Configuration options for Azure Key Vault integration for JWT signing
/// </summary>
public sealed class AzureKeyVaultOptions
{
    public const string SectionName = "AzureKeyVault";

    /// <summary>
    /// The URI of the Azure Key Vault instance
    /// </summary>
    [Required]
    public string VaultUri { get; set; } = string.Empty;

    /// <summary>
    /// The name of the key used for JWT signing
    /// </summary>
    [Required]
    public string JwtSigningKeyName { get; set; } = string.Empty;

    /// <summary>
    /// The tenant ID for Azure Active Directory authentication
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// The client ID for service principal authentication
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// The client secret for service principal authentication
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Whether to use Managed Identity for authentication (default: true in production)
    /// </summary>
    public bool UseManagedIdentity { get; set; } = true;

    /// <summary>
    /// Key rotation overlap window in hours (default: 48 hours)
    /// </summary>
    [Range(24, 168)]
    public int KeyRotationOverlapHours { get; set; } = 48;

    /// <summary>
    /// Cache duration for signing keys in minutes (default: 60 minutes)
    /// </summary>
    [Range(5, 1440)]
    public int KeyCacheDurationMinutes { get; set; } = 60;
}