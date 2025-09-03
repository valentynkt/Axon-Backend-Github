namespace Axon.Modules.Identity.Application.Queries.GetCurrentUser;

/// <summary>
/// DTO representing a principal's profile information.
/// Contains only domain-backed data with no stubs or fabricated values.
/// Uses primitive types only for JSON serialization safety.
/// </summary>
public record PrincipalProfileDto
{
    public required string AxonId { get; init; }
    public required string PrincipalType { get; init; }
    public required string PreferredLanguage { get; init; }
    public required string RiskTier { get; init; }
    public string? PrimaryEmailHash { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}