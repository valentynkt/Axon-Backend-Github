using Axon.Modules.Identity.Application.DTOs.Responses;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Identity.Application.Queries.GetWallet;

/// <summary>
/// Query to retrieve a wallet by its unique identifier.
/// Returns wallet data with tags redacted based on caller authorization.
/// </summary>
public sealed record GetWalletQuery(string WalletId) : IRequest<Result<WalletDto, Error>>;