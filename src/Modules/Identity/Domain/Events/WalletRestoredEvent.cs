namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a soft-deleted wallet is restored.
/// </summary>
public sealed record WalletRestoredEvent(
    WalletId WalletId,
    DateTimeOffset RestoredAt
) : DomainEvent;