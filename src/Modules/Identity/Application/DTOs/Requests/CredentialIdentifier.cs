using Axon.Modules.Identity.Domain.Enums;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.DTOs.Requests;

/// <summary>
/// Represents one-of credential identification paths for credential operations.
/// Either CredentialId or the combination of ProviderType+Issuer+Subject must be provided.
/// </summary>
public sealed record CredentialIdentifier(
    IdentityCredentialId? CredentialId = null,
    string? ProviderType = null,
    string? Issuer = null,
    string? Subject = null
)
{
    /// <summary>
    /// Validates that exactly one identification path is provided.
    /// </summary>
    public bool IsValid()
    {
        var hasCredentialId = CredentialId is not null;
        var hasProviderPath = !string.IsNullOrEmpty(ProviderType) && 
                             !string.IsNullOrEmpty(Issuer) && 
                             !string.IsNullOrEmpty(Subject);
        
        return hasCredentialId ^ hasProviderPath; // Exactly one must be true
    }
};