namespace Axon.Modules.Identity.Application.Queries.GetCurrentUser;

/// <summary>
/// DTO representing a wallet owned by the current principal.
/// Contains ownership-specific data without exposing internal metadata or tags.
/// Uses primitive types only for JSON serialization safety.
/// </summary>
public record OwnedWalletDto
{
    public required string WalletId { get; init; }
    public required string Address { get; init; }
    public required string ChainId { get; init; }
    public required string ProofType { get; init; }
    public required string AccessMode { get; init; }
    public required string OwnershipState { get; init; }
    public required DateTimeOffset FirstLinkedAt { get; init; }
    public DateTimeOffset? LastVerifiedAt { get; init; }
    public string? Label { get; init; }
}