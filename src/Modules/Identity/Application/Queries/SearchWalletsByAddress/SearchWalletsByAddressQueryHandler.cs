using Axon.Modules.Identity.Application.Common.Mappers;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Queries.SearchWalletsByAddress;

/// <summary>
/// Handler for searching wallets by partial address match.
/// Returns slim results for UI autocomplete scenarios.
/// </summary>
public sealed class SearchWalletsByAddressQueryHandler : IRequestHandler<SearchWalletsByAddressQuery, Result<IReadOnlyList<WalletSearchResultDto>, Error>>
{
    private readonly IWalletReadRepository _walletRepository;
    private readonly ILogger<SearchWalletsByAddressQueryHandler> _logger;

    public SearchWalletsByAddressQueryHandler(
        IWalletReadRepository walletRepository,
        ILogger<SearchWalletsByAddressQueryHandler> logger)
    {
        _walletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<IReadOnlyList<WalletSearchResultDto>, Error>> Handle(
        SearchWalletsByAddressQuery request, 
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            ChainId? chainId = null;
            if (!string.IsNullOrEmpty(request.ChainId))
            {
                try
                {
                    chainId = ChainId.From(request.ChainId);
                }
                catch (Exception)
                {
                    _logger.LogWarning("Invalid chain ID format in search: {ChainId}", request.ChainId);
                    return Result.Failure<IReadOnlyList<WalletSearchResultDto>, Error>(
                        Error.Validation("Invalid chain ID.", "IDENTITY.WALLET.CHAIN.INVALID"));
                }
            }

            // Use repository's optimized search method (trim input to prevent whitespace issues)
            var wallets = await _walletRepository.SearchByAddressAsync(
                request.Query?.Trim() ?? string.Empty,
                chainId,
                includeDeleted: false,
                take: request.Take,
                cancellationToken);

            // Map to slim DTOs (no tags for performance and privacy)
            var searchResults = wallets
                .Select(IdentityDtoMapper.ToWalletSearchResultDto)
                .ToList()
                .AsReadOnly();

            _logger.LogDebug("Address search returned {Count} results for query: {Query}", 
                searchResults.Count, request.Query);

            return Result.Success<IReadOnlyList<WalletSearchResultDto>, Error>(searchResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search wallets by address: {Query}", request.Query);
            return Result.Failure<IReadOnlyList<WalletSearchResultDto>, Error>(
                Error.Internal("Failed to search wallets.", "IDENTITY.WALLET.SEARCH.FAILED"));
        }
    }
}