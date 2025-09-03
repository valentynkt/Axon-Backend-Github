using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;

/// <summary>
/// Status enumeration for UpsertPrincipalFromCredential operation.
/// </summary>
public enum UpsertPrincipalStatus
{
    Created,
    UpdatedLastSeen,
    LinkedWithWallet,
    ConflictOwnedByOther
}

/// <summary>
/// Response for UpsertPrincipalFromCredential command.
/// </summary>
public sealed record UpsertPrincipalResponse(
    PrincipalDto Principal,
    CredentialDto Credential,
    WalletOwnershipDto? AttachedWallet,
    bool? AppliedDefault,
    string? DefaultNotAppliedReason,
    UpsertPrincipalStatus Status,
    ConflictInfo? Conflict
);

/// <summary>
/// Information about conflicts when they occur.
/// </summary>
public sealed record ConflictInfo(
    AxonId ExistingOwnerPrincipalId
);