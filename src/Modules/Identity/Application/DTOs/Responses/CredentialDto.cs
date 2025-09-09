using Axon.Modules.Identity.Domain.Enums;
namespace Axon.Modules.Identity.Application.DTOs.Responses;

/// <summary>
/// Data transfer object representing an identity credential.
/// Uses primitive types only for JSON serialization safety.
/// </summary>
public sealed record CredentialDto(
    string CredentialId,
    string ProviderType,
    string Issuer,
    string Subject,
    string? EnvironmentId,
    DateTimeOffset VerifiedAt,
    DateTimeOffset LastSeenAt,
    string[] MetadataKeys
);