using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a wallet ownership is linked to a principal.
/// </summary>
public sealed record WalletOwnershipLinkedEvent(
    AxonId AxonId,
    WalletId WalletId,
    WalletOwnershipId OwnershipId,
    string ProofType,
    string AccessMode,
    ChainId ChainId,
    DateTimeOffset LinkedAt
) : DomainEvent;