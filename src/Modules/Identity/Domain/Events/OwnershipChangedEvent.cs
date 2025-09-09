namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Consolidated event for all ownership-related changes.
/// Replaces: WalletOwnershipLinked, WalletOwnershipUnlinked, 
/// WalletOwnershipVerified, WalletOwnershipConflictSkipped, WalletAccessModeUpdated.
/// </summary>
public sealed record OwnershipChangedEvent(
    string AxonId,
    string WalletId,
    string ChangeType, // "linked", "unlinked", "verified", "conflict_skipped", "access_mode_updated"
    string? OwnershipId = null,
    string? ChainId = null,
    string? ProofType = null,
    string? AccessMode = null,
    string? PreviousAccessMode = null,
    string? VerificationMethod = null,
    DateTime? EventOccurredAt = null
) : DomainEvent(EventOccurredAt ?? DateTime.UtcNow);