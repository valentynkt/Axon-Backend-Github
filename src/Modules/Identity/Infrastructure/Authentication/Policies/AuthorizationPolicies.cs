using Microsoft.AspNetCore.Authorization;

namespace Axon.Modules.Identity.Infrastructure.Authentication.Policies;

/// <summary>
/// Authorization policies for the Axon API
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Policy name for authenticated users
    /// </summary>
    public const string Authenticated = "Authenticated";
    
    /// <summary>
    /// Configures authorization policies for the application
    /// </summary>
    /// <param name="options">Authorization options to configure</param>
    public static void Configure(AuthorizationOptions options)
    {
        // Single policy for authenticated users using Dynamic JWT
        options.AddPolicy(Authenticated, policy =>
            policy.RequireAuthenticatedUser()
                  .AddAuthenticationSchemes(AuthenticationSchemes.DynamicJwt));
    }
}