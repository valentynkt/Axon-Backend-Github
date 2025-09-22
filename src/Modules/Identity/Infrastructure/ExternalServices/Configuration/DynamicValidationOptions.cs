using System.ComponentModel.DataAnnotations;

namespace Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;

/// <summary>
/// Enhanced configuration for Dynamic JWT validation with per-partner audience allowlists
/// </summary>
public sealed class DynamicValidationOptions
{
    public const string SectionName = "DynamicValidation";

    /// <summary>
    /// Mapping of Dynamic environmentId to our environment name
    /// </summary>
    public Dictionary<string, string> EnvironmentMapping { get; set; } = new();

    /// <summary>
    /// Per-partner audience allowlists, keyed by API key or partner ID
    /// </summary>
    public Dictionary<string, List<string>> PartnerAudienceAllowlist { get; set; } = new();

    /// <summary>
    /// Default allowed audiences if no partner-specific list is configured
    /// </summary>
    public List<string> DefaultAllowedAudiences { get; set; } = new();

    /// <summary>
    /// Whether to enforce audience validation (default: true)
    /// </summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>
    /// Clock skew tolerance in seconds (default: 60, max: 60)
    /// </summary>
    [Range(0, 60)]
    public int ClockSkewSeconds { get; set; } = 60;

    /// <summary>
    /// JWKS cache duration in minutes (default: 20, range: 10-30)
    /// </summary>
    [Range(10, 30)]
    public int JwksCacheMinutes { get; set; } = 20;

    /// <summary>
    /// Enable background refresh of JWKS cache (default: true)
    /// </summary>
    public bool EnableBackgroundRefresh { get; set; } = true;

    /// <summary>
    /// Background refresh interval in minutes (default: 15)
    /// </summary>
    [Range(5, 25)]
    public int BackgroundRefreshMinutes { get; set; } = 15;
}