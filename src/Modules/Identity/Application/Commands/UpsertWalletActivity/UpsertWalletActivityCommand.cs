using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.Commands.UpsertWalletActivity;

/// <summary>
/// Single write surface for wallet "touch", meta patch, and tag add/remove 
/// (also register on the fly if needed).
/// Used by Dynamic JWT exchange orchestrator.
/// </summary>
public sealed record UpsertWalletActivityCommand(
    string? IdempotencyKey,
    string? CorrelationId,
    WalletId? WalletId,
    ChainId? ChainId,
    string? RawAddress,
    DateTimeOffset? ObservedAt,
    Dictionary<string, object>? MetaPatch,
    string[]? TagsToAdd,
    string[]? TagsToRemove
) : IdentityIdempotentCommand<WalletActivityResponse>
{
    /// <summary>
    /// Override idempotency key if provided by caller.
    /// </summary>
    public override string? GetExplicitIdempotencyKey() => IdempotencyKey;

    /// <summary>
    /// Validates that either WalletId or (ChainId + RawAddress) is provided.
    /// </summary>
    public bool IsValid()
    {
        var hasWalletId = WalletId is not null;
        var hasChainAddress = ChainId is not null && !string.IsNullOrEmpty(RawAddress);
        
        return hasWalletId ^ hasChainAddress; // Exactly one must be true
    }
};