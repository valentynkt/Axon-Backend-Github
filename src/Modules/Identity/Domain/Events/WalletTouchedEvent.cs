namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a wallet's LastSeenAt timestamp is updated.
/// </summary>
public sealed record WalletTouchedEvent(
    WalletId WalletId,
    DateTimeOffset LastSeenAt
) : DomainEvent;