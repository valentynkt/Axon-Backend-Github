namespace Axon.Api.Contracts.V1.Auth;

/// <summary>
/// Request for getting current user information
/// Authentication via JWT token in Authorization header
/// </summary>
public sealed record GetCurrentUserRequestDto
{
    /// <summary>
    /// Placeholder property to satisfy FastEndpoints Swagger requirements
    /// This endpoint uses JWT from Authorization header, not request body
    /// </summary>
    public string? Placeholder { get; init; }
}