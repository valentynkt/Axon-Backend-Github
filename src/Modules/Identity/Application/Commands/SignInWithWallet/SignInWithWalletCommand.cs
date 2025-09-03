using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.Commands.SignInWithWallet;

/// <summary>
/// Command for wallet-first sign-in flow that atomically verifies signature and links/creates principal.
/// Supports idempotent operation with challenge-based signature verification.
/// </summary>
public sealed record SignInWithWalletCommand(
    ChainId ChainId,
    string RawAddress,
    string Signature,
    string ChallengeId,
    string? Label = null,
    bool SetAsDefault = false,
    string? AccessMode = null,
    string? IdempotencyKey = null,
    string? CorrelationId = null
) : IdentityIdempotentCommand<SignInWithWalletResponse>
{
    /// <summary>
    /// Override idempotency key if provided by caller.
    /// </summary>
    public override string? GetExplicitIdempotencyKey() => IdempotencyKey;
};