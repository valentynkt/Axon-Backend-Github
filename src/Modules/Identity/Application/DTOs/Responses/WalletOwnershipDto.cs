namespace Axon.Modules.Identity.Application.DTOs.Responses;

/// <summary>
/// Data transfer object representing wallet ownership.
/// Contains ownership details for the authenticated caller only.
/// </summary>
public sealed record WalletOwnershipDto(
    string OwnershipId,
    string AxonId,
    string WalletId,
    string ChainId,
    string ProofType,
    string AccessMode,
    string State,
    DateTimeOffset FirstLinkedAt,
    DateTimeOffset? LastVerifiedAt,
    string? Label
);