namespace Axon.Api.Contracts.V1.Identity.Authentication;

/// <summary>
/// Request for exchanging a Dynamic JWT token
/// The JWT token is extracted from the Authorization header by the endpoint
/// </summary>
public sealed record ExchangeDynamicTokenRequest
{
    // Empty request - JWT comes from Authorization header
}