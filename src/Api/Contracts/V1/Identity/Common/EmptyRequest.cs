namespace Axon.Api.Contracts.V1.Identity.Common;

/// <summary>
/// Represents an empty request for GET endpoints that do not require request parameters
/// </summary>
public sealed record EmptyRequest
{
    /// <summary>
    /// Optional placeholder property for Swagger/OpenAPI compatibility.
    /// This property is not used and should remain null.
    /// </summary>
    public string? Placeholder { get; init; }
}