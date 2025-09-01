namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when wallet ownership linking is skipped due to conflicts.
/// This occurs when a wallet is already owned by another principal and the conflict
/// resolution strategy is to skip rather than fail.
/// </summary>
public sealed record WalletOwnershipConflictSkippedEvent(
    AxonId RequestedByPrincipalId,
    AxonId ExistingOwnerPrincipalId,
    WalletId WalletId,
    string ConflictReason,
    DateTimeOffset SkippedAt,
    string? ResolutionStrategy = null
) : DomainEvent;