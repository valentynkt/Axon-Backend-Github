namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a wallet is soft deleted.
/// </summary>
public sealed record WalletSoftDeletedEvent(
    WalletId WalletId,
    DateTimeOffset DeletedAt
) : DomainEvent;