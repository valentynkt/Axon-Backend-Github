using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;

/// <summary>
/// Status enumeration for RemoveWalletLink operation.
/// </summary>
public enum RemoveWalletStatus
{
    Unlinked,
    NotOwned
}

/// <summary>
/// Response for RemoveWalletLink command.
/// </summary>
public sealed record RemoveWalletResponse(
    AxonId AxonId,
    WalletId WalletId,
    RemoveWalletStatus Status
);