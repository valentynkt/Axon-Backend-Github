namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a tag is added to a wallet.
/// </summary>
public sealed record WalletTaggedEvent(
    WalletId WalletId,
    string Tag
) : DomainEvent;