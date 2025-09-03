using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Application.Commands.RemoveWalletLink;

/// <summary>
/// Unlinks ownership from a principal and clears any chain default that pointed at it.
/// </summary>
public sealed record RemoveWalletLinkCommand(
    string? CorrelationId,
    AxonId AxonId,
    WalletId WalletId
) : IdentityBaseCommand<RemoveWalletResponse>;