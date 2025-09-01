namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when the default wallet for a chain is changed.
/// </summary>
public sealed record DefaultWalletChangedEvent(
    AxonId AxonId,
    string Chain,
    long NewWalletId,
    long? PreviousWalletId,
    DateTimeOffset ChangedAt
) : DomainEvent;