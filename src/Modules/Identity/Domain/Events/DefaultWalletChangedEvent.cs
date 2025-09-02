using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when the default wallet for a chain is changed.
/// </summary>
public sealed record DefaultWalletChangedEvent(
    AxonId AxonId,
    ChainId ChainId,
    WalletId NewWalletId,
    WalletId? PreviousWalletId,
    DateTimeOffset ChangedAt
) : DomainEvent;