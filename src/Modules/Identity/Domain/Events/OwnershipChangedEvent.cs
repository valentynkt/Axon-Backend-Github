using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Consolidated event for all ownership-related changes.
/// Replaces: WalletOwnershipLinked, WalletOwnershipUnlinked, 
/// WalletOwnershipVerified, WalletOwnershipConflictSkipped, WalletAccessModeUpdated.
/// </summary>
public sealed record OwnershipChangedEvent : DomainEvent
{
    public AxonId PrincipalId { get; init; }
    public WalletId WalletId { get; init; }
    public string ChangeType { get; init; } // "linked", "removed", "verified", "access_mode_updated", "status_updated"
    public string? AccessMode { get; init; }
    public string? Status { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }

    public OwnershipChangedEvent(
        AxonId principalId,
        WalletId walletId,
        string changeType,
        string? accessMode = null,
        string? status = null,
        Dictionary<string, string>? metadata = null,
        DateTime? eventOccurredAt = null) : base(eventOccurredAt ?? DateTime.UtcNow)
    {
        PrincipalId = principalId;
        WalletId = walletId;
        ChangeType = changeType;
        AccessMode = accessMode;
        Status = status;
        Metadata = metadata;
    }
}