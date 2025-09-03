using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a wallet ownership access mode is updated.
/// </summary>
public sealed record WalletAccessModeUpdatedEvent(
    AxonId AxonId,
    WalletId WalletId,
    WalletOwnershipId OwnershipId,
    string NewAccessMode,
    string PreviousAccessMode,
    DateTimeOffset UpdatedAt
) : DomainEvent;