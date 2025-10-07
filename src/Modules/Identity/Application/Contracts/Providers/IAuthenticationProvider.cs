namespace Axon.Modules.Identity.Application.Contracts.Providers;

using Domain.Entities;
using CSharpFunctionalExtensions;

/// <summary>
/// Strategy interface for different authentication providers.
/// Each provider handles a specific authentication method (wallet, Dynamic.xyz, etc.)
/// </summary>
public interface IAuthenticationProvider
{
    /// <summary>
    /// The type of authentication this provider handles (e.g., "wallet", "dynamic")
    /// </summary>
    string ProviderType { get; }

    /// <summary>
    /// Determines if this provider can handle the given authentication request
    /// </summary>
    bool CanHandle(AuthenticationRequest request);

    /// <summary>
    /// Process authentication request and return authenticated user data
    /// </summary>
    Task<Result<AuthenticationData, Error>> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Base authentication request that all specific requests inherit from
/// </summary>
public abstract record AuthenticationRequest
{
    public abstract string RequestType { get; }
}

/// <summary>
/// Wallet-based authentication request for signature verification
/// </summary>
public sealed record WalletAuthenticationRequest(
    string ChainId,
    string Address,
    string SignedMessage,
    string Signature) : AuthenticationRequest
{
    public override string RequestType => "wallet";
}

/// <summary>
/// Dynamic.xyz token exchange request for JWT validation
/// </summary>
public sealed record DynamicExchangeRequest(
    string Token,
    string? EnvironmentId = null) : AuthenticationRequest
{
    public override string RequestType => "dynamic";
}

/// <summary>
/// Pre-validated claims from middleware (for already validated tokens)
/// </summary>
public sealed record ValidatedClaimsRequest(
    Dictionary<string, object> Claims,
    string ProviderType,
    string Subject) : AuthenticationRequest
{
    public override string RequestType => "validated";
}

/// <summary>
/// Successful authentication data returned by providers
/// </summary>
public sealed record AuthenticationData(
    AxonUserAuth User,
    string ProviderType,
    Dictionary<string, object> AdditionalClaims,
    DateTime? TokenExpiresAt = null);

/// <summary>
/// Final authentication response with generated tokens
/// </summary>
public sealed record AuthenticationResponse(
    string AccessToken,
    string? RefreshToken,
    Guid UserId,
    string ProviderType,
    DateTime ExpiresAt,
    DateTime? RefreshExpiresAt = null,
    Dictionary<string, object>? AdditionalData = null);