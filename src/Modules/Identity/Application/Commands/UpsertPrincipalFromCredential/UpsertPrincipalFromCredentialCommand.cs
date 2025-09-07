using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.DTOs.Requests;
using Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;

namespace Axon.Modules.Identity.Application.Commands.UpsertPrincipalFromCredential;

/// <summary>
/// Resolves or creates a human principal from a credential and updates last-seen if found.
/// Wallet operations should be performed separately through dedicated wallet commands.
/// </summary>
public sealed record UpsertPrincipalFromCredentialCommand(
    string? IdempotencyKey,
    string? CorrelationId,
    string ProviderType,
    string Issuer,
    string Subject,
    string? EnvironmentId,
    Dictionary<string, object>? CredentialMetadata,
    string? PrimaryEmailHash
) : IdentityIdempotentCommand<UpsertPrincipalResponse>
{
    /// <summary>
    /// Override idempotency key if provided by caller.
    /// </summary>
    public override string? GetExplicitIdempotencyKey() => IdempotencyKey;
};