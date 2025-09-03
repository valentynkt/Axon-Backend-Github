using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.DTOs.Requests;
using Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;

namespace Axon.Modules.Identity.Application.Commands.UpsertPrincipalFromCredential;

/// <summary>
/// Resolves or creates a human principal from a credential, updates last-seen if found,
/// optionally attaches and (optionally) verifies a wallet in one shot.
/// </summary>
public sealed record UpsertPrincipalFromCredentialCommand(
    string? IdempotencyKey,
    string? CorrelationId,
    string ProviderType,
    string Issuer,
    string Subject,
    string? EnvironmentId,
    Dictionary<string, object>? CredentialMetadata,
    string? PrimaryEmailHash,
    AttachWalletRequest? AttachWallet
) : IdentityIdempotentCommand<UpsertPrincipalResponse>
{
    /// <summary>
    /// Override idempotency key if provided by caller.
    /// </summary>
    public override string? GetExplicitIdempotencyKey() => IdempotencyKey;
};