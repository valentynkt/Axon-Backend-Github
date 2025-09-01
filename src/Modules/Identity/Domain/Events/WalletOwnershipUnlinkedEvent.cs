namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a wallet ownership is unlinked from a principal.
/// </summary>
public sealed record WalletOwnershipUnlinkedEvent(
    AxonId AxonId,
    long WalletId,
    WalletOwnershipId OwnershipId,
    DateTimeOffset UnlinkedAt
) : DomainEvent;