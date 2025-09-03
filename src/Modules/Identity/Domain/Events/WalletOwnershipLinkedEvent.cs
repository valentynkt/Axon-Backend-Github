using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a wallet ownership is linked to a principal.
/// </summary>
public sealed record WalletOwnershipLinkedEvent(
    string AxonId,
    string WalletId,
    string OwnershipId,
    string ProofType,
    string AccessMode,
    string ChainId,
    DateTimeOffset LinkedAt
) : DomainEvent;