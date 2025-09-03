using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.DTOs.Requests;
using Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.Commands.RevokeCredential;

/// <summary>
/// Soft deletes a credential identified by one of two paths:
/// either by CredentialId or by ProviderType + Issuer + Subject combination.
/// </summary>
public sealed record RevokeCredentialCommand(
    string? CorrelationId,
    AxonId AxonId,
    CredentialIdentifier CredentialIdentifier,
    string? Reason
) : IdentityBaseCommand<RevokeCredentialResponse>;