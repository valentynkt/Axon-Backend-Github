using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Tokens;

namespace Axon.Modules.Identity.Infrastructure.Authentication.Options;

/// <summary>
/// Options for Axon internal JWT authentication handler
/// </summary>
public sealed class AxonJwtAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// The realm to use in WWW-Authenticate challenge headers
    /// </summary>
    public string Realm { get; set; } = "Axon API";

    /// <summary>
    /// Valid issuers for Axon JWT tokens
    /// </summary>
    public ICollection<string> ValidIssuers { get; set; } = new List<string>();

    /// <summary>
    /// Valid audiences for Axon JWT tokens
    /// </summary>
    public ICollection<string> ValidAudiences { get; set; } = new List<string>();

    /// <summary>
    /// Whether to validate audience claims
    /// </summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>
    /// Signing keys for token validation
    /// </summary>
    public ICollection<SecurityKey> SigningKeys { get; set; } = new List<SecurityKey>();

    /// <summary>
    /// Clock skew tolerance for token validation
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(60);
}