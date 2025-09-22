namespace Axon.Modules.Identity.Infrastructure.Authentication;

/// <summary>
/// Defines authentication scheme constants for JWT authentication
/// </summary>
public static class AuthenticationSchemes
{
    /// <summary>
    /// The Dynamic JWT authentication scheme for external token validation
    /// </summary>
    public const string DynamicJwt = "DynamicJwt";

    /// <summary>
    /// The Axon JWT authentication scheme for internal token validation
    /// </summary>
    public const string AxonJwt = "AxonJwt";
}