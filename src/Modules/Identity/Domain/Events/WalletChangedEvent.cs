namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Consolidated event for all wallet-related changes.
/// Replaces: WalletRegistered, WalletSoftDeleted, WalletRestored, 
/// WalletMetaUpdated, WalletTouched, WalletTagged, WalletUntagged, WalletLabelUpdated.
/// </summary>
public sealed record WalletChangedEvent : DomainEvent
{
    public string WalletId { get; init; }
    public string ChangeType { get; init; } // "registered", "deleted", "restored", "meta_updated", "touched", "tagged", "untagged", "labeled"
    public string? ChainId { get; init; }
    public string? Address { get; init; }
    public string? DisplayName { get; init; }
    public string? Tag { get; init; }
    public string? Label { get; init; }

    public WalletChangedEvent(
        string walletId,
        string changeType,
        string? chainId = null,
        string? address = null,
        string? displayName = null,
        string? tag = null,
        string? label = null,
        DateTime? occurredAt = null) : base(occurredAt ?? DateTime.UtcNow)
    {
        WalletId = walletId;
        ChangeType = changeType;
        ChainId = chainId;
        Address = address;
        DisplayName = displayName;
        Tag = tag;
        Label = label;
    }
}