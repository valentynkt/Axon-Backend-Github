namespace Axon.Api.Contracts.V1.Auth;

/// <summary>
/// Request for token exchange endpoint
/// JWT token is provided via Authorization header, not request body
/// </summary>
public sealed record ExchangeTokenRequestDto
{
    /// <summary>
    /// Placeholder property to satisfy FastEndpoints Swagger requirements
    /// This endpoint uses JWT from Authorization header, not request body
    /// </summary>
    public string? Placeholder { get; init; }
}