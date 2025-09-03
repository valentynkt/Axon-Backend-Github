using Axon.Modules.Identity.Application.DTOs.Responses;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Identity.Application.Queries.GetMyWalletOwnership;

/// <summary>
/// Query to check if the authenticated caller owns a specific wallet.
/// Returns ownership details or indicates the wallet is not owned by caller.
/// Supports lookup by wallet ID or by chain/address coordinates.
/// </summary>
public sealed record GetMyWalletOwnershipQuery : IRequest<Result<WalletOwnershipStatusDto, Error>>
{
    public string? WalletId { get; init; }
    public string? ChainId { get; init; }
    public string? RawAddress { get; init; }

    /// <summary>
    /// Creates a query for checking ownership by wallet ID.
    /// </summary>
    public static GetMyWalletOwnershipQuery ByWalletId(string walletId) =>
        new() { WalletId = walletId };

    /// <summary>
    /// Creates a query for checking ownership by chain and address coordinates.
    /// </summary>
    public static GetMyWalletOwnershipQuery ByCoordinates(string chainId, string rawAddress) =>
        new() { ChainId = chainId, RawAddress = rawAddress };

    /// <summary>
    /// Validates that either wallet ID or both chain ID and address are provided.
    /// </summary>
    public bool IsValid =>
        (!string.IsNullOrEmpty(WalletId)) ||
        (!string.IsNullOrEmpty(ChainId) && !string.IsNullOrEmpty(RawAddress));
}