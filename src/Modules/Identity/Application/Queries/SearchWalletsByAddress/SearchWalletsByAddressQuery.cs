using Axon.Modules.Identity.Application.DTOs.Responses;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Identity.Application.Queries.SearchWalletsByAddress;

/// <summary>
/// Query to search wallets by partial address match for UI autocomplete scenarios.
/// Returns slim results without tags for performance and privacy.
/// </summary>
public sealed record SearchWalletsByAddressQuery(
    string Query,
    string? ChainId = null,
    int Take = 10
) : IRequest<Result<IReadOnlyList<WalletSearchResultDto>, Error>>;