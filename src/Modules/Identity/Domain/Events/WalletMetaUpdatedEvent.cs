namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when wallet metadata is updated.
/// </summary>
public sealed record WalletMetaUpdatedEvent(
    WalletId WalletId,
    string[] KeysChanged
) : DomainEvent;