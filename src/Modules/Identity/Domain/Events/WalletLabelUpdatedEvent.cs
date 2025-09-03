using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when a wallet ownership label is updated.
/// </summary>
public sealed record WalletLabelUpdatedEvent(
    AxonId AxonId,
    WalletId WalletId,
    WalletOwnershipId OwnershipId,
    string? NewLabel,
    string? PreviousLabel,
    DateTimeOffset UpdatedAt
) : DomainEvent;