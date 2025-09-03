using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;

/// <summary>
/// Status enumeration for RevokeCredential operation.
/// </summary>
public enum RevokeCredentialStatus
{
    Revoked,
    AlreadyRevoked
}

/// <summary>
/// Response for RevokeCredential command.
/// </summary>
public sealed record RevokeCredentialResponse(
    AxonId AxonId,
    IdentityCredentialId CredentialId,
    RevokeCredentialStatus Status
);