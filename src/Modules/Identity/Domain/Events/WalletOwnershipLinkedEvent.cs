namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a wallet ownership is linked to a principal.
/// </summary>
public sealed record WalletOwnershipLinkedEvent(
    AxonId AxonId,
    long WalletId,
    WalletOwnershipId OwnershipId,
    string ProofType,
    string AccessMode,
    string Chain,
    DateTimeOffset LinkedAt
) : DomainEvent;