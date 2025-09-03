using Axon.Modules.Identity.Application.DTOs.Responses;

namespace Axon.Modules.Identity.Application.Queries.GetPrincipalByCredential;

/// <summary>
/// Response DTO containing both principal and the matching credential.
/// Used when resolving a principal by their identity credential.
/// </summary>
/// <param name="Principal">The principal that owns the credential</param>
/// <param name="Credential">The credential that matched the query</param>
public sealed record PrincipalWithCredentialDto(
    PrincipalDto Principal,
    CredentialDto Credential
);