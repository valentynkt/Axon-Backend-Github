using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Consolidated event for all wallet-related changes.
/// Replaces: WalletRegistered, WalletSoftDeleted, WalletRestored, 
/// WalletMetaUpdated, WalletTouched, WalletTagged, WalletUntagged, WalletLabelUpdated.
/// </summary>
public sealed record WalletChangedEvent : DomainEvent
{
    public WalletId WalletId { get; init; }
    public string PropertyChanged { get; init; } // "ownership", "ownershipStatus", "accessMode", "lastSeen"
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }

    public WalletChangedEvent(
        WalletId walletId,
        string propertyChanged,
        string? oldValue = null,
        string? newValue = null,
        Dictionary<string, string>? metadata = null,
        DateTime? occurredAt = null) : base(occurredAt ?? DateTime.UtcNow)
    {
        WalletId = walletId;
        PropertyChanged = propertyChanged;
        OldValue = oldValue;
        NewValue = newValue;
        Metadata = metadata;
    }
}