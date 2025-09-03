using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when the default wallet for a chain is changed or cleared.
/// When NewWalletId is null, it indicates the default was cleared (no default wallet for this chain).
/// </summary>
public sealed record DefaultWalletChangedEvent(
    string AxonId,
    string ChainId,
    string? NewWalletId,
    string? PreviousWalletId,
    DateTimeOffset ChangedAt
) : DomainEvent;