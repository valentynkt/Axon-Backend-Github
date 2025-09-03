using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.Commands.EnsureWalletLinked;

/// <summary>
/// Given a principal and wallet coordinates, ensures the wallet exists and is linked 
/// with the desired state (verify, default, label, access mode).
/// </summary>
public sealed record EnsureWalletLinkedCommand(
    string? IdempotencyKey,
    string? CorrelationId,
    AxonId AxonId,
    ChainId ChainId,
    string RawAddress,
    string ProofType,
    string? AccessMode,
    string? Label,
    bool Verify = true,
    bool SetAsDefault = false
) : IdentityIdempotentCommand<EnsureWalletResponse>
{
    /// <summary>
    /// Override idempotency key if provided by caller.
    /// </summary>
    public override string? GetExplicitIdempotencyKey() => IdempotencyKey;
};