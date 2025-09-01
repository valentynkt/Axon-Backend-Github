namespace Axon.Modules.Identity.Domain.Events;

/// <summary>
/// Domain event raised when an identity credential is linked to a principal.
/// </summary>
public sealed record IdentityCredentialLinkedEvent(
    AxonId AxonId,
    IdentityCredentialId CredentialId,
    string ProviderType,
    string Issuer,
    string Subject,
    string? EnvironmentId,
    DateTimeOffset LinkedAt
) : DomainEvent;