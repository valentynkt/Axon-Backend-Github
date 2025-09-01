namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when wallet ownership is verified through proof validation.
/// </summary>
public sealed record WalletOwnershipVerifiedEvent(
    AxonId AxonId,
    long WalletId,
    WalletOwnershipId OwnershipId,
    string ProofType,
    DateTimeOffset VerifiedAt,
    string? VerificationMethod = null
) : DomainEvent;