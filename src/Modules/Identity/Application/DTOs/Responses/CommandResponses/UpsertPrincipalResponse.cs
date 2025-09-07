using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;

/// <summary>
/// Status enumeration for UpsertPrincipalFromCredential operation.
/// </summary>
public enum UpsertPrincipalStatus
{
    Created,
    UpdatedLastSeen
}

/// <summary>
/// Response for UpsertPrincipalFromCredential command.
/// Wallet operations should be performed separately through dedicated wallet commands.
/// </summary>
public sealed record UpsertPrincipalResponse(
    PrincipalDto Principal,
    CredentialDto Credential,
    UpsertPrincipalStatus Status
);