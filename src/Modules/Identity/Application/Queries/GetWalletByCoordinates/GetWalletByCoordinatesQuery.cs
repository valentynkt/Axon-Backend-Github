using Axon.Modules.Identity.Application.DTOs.Responses;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Identity.Application.Queries.GetWalletByCoordinates;

/// <summary>
/// Query to retrieve a wallet by chain ID and address coordinates.
/// Returns wallet data with tags redacted based on caller authorization.
/// </summary>
public sealed record GetWalletByCoordinatesQuery(
    string ChainId, 
    string RawAddress
) : IRequest<Result<WalletDto, Error>>;