using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a new wallet is registered.
/// </summary>
public sealed record WalletRegisteredEvent(
    WalletId WalletId,
    ChainId ChainId,
    Address Address,
    DateTimeOffset FirstSeenAt
) : DomainEvent;