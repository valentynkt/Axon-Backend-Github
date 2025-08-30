namespace Axon.Api.Contracts.V1.Identity.Authentication;

/// <summary>
/// Represents detailed user profile information retrieved from authentication provider
/// </summary>
public record UserProfileDto
{
    /// <summary>
    /// Gets the Axon internal user identifier
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// Gets the Dynamic.xyz user identifier
    /// </summary>
    public required Guid DynamicUserId { get; init; }
    
    /// <summary>
    /// Gets the user's email address
    /// </summary>
    public required string Email { get; init; }
    
    /// <summary>
    /// Gets the user's display name
    /// </summary>
    public string? DisplayName { get; init; }
    
    /// <summary>
    /// Gets the user's username
    /// </summary>
    public string? Username { get; init; }
    
    /// <summary>
    /// Gets the timestamp of the user's first visit
    /// </summary>
    public DateTime? FirstVisit { get; init; }
    
    /// <summary>
    /// Gets the timestamp of the user's last visit
    /// </summary>
    public DateTime? LastVisit { get; init; }
    
    /// <summary>
    /// Gets the user metadata from Dynamic.xyz including preferences and custom attributes
    /// </summary>
    public required IReadOnlyDictionary<string, object> Metadata { get; init; }
}