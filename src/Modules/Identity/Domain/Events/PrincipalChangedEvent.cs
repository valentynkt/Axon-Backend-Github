using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Consolidated event for all principal-related changes.
/// Replaces: PrincipalCreated, PrincipalSoftDeleted, PrincipalRestored, 
/// ProfileLanguageChanged, ProfileRiskTierChanged, DefaultWalletChanged, and other profile events.
/// </summary>
public sealed record PrincipalChangedEvent : DomainEvent
{
    public AxonUserId PrincipalId { get; init; }
    public string PropertyChanged { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }

    public PrincipalChangedEvent(
        AxonUserId principalId,
        string propertyChanged,
        string? oldValue = null,
        string? newValue = null,
        Dictionary<string, string>? metadata = null,
        DateTime? eventOccurredAt = null) : base(eventOccurredAt ?? DateTime.UtcNow)
    {
        PrincipalId = principalId;
        PropertyChanged = propertyChanged;
        OldValue = oldValue;
        NewValue = newValue;
        Metadata = metadata;
    }
}