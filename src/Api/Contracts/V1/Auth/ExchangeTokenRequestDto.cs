namespace Axon.Api.Contracts.V1.Auth;

/// <summary>
/// Request for token exchange endpoint
/// JWT token is provided via Authorization header, not request body
/// </summary>
public sealed record ExchangeTokenRequestDto;