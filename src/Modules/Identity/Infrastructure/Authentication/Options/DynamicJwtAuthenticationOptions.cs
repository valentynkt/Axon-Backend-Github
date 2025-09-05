using Microsoft.AspNetCore.Authentication;

namespace Axon.Modules.Identity.Infrastructure.Authentication.Options;

/// <summary>
/// Options for Dynamic.xyz JWT authentication handler
/// </summary>
public sealed class DynamicJwtAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// The realm to use in WWW-Authenticate challenge headers
    /// </summary>
    public string Realm { get; set; } = "Axon API";
}