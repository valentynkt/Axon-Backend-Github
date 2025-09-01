namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a tag is removed from a wallet.
/// </summary>
public sealed record WalletUntaggedEvent(
    WalletId WalletId,
    string Tag
) : DomainEvent;