using Axon.Modules.Identity.Application.Common.Mappers;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Application.Specifications.Wallets;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Queries.GetWalletByCoordinates;

/// <summary>
/// Handler for retrieving a wallet by chain and address coordinates.
/// </summary>
public sealed class GetWalletByCoordinatesQueryHandler : IRequestHandler<GetWalletByCoordinatesQuery, Result<WalletDto, Error>>
{
    private readonly IWalletReadRepository _walletRepository;
    private readonly IWalletAuthorizationService _authorizationService;
    private readonly ILogger<GetWalletByCoordinatesQueryHandler> _logger;

    public GetWalletByCoordinatesQueryHandler(
        IWalletReadRepository walletRepository,
        IWalletAuthorizationService authorizationService,
        ILogger<GetWalletByCoordinatesQueryHandler> logger)
    {
        _walletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<WalletDto, Error>> Handle(GetWalletByCoordinatesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Create domain value objects with normalization
            ChainId chainId;
            try
            {
                chainId = ChainId.From(request.ChainId);
            }
            catch (Exception)
            {
                _logger.LogWarning("Invalid chain ID format: {ChainId}", request.ChainId);
                return Result.Failure<WalletDto, Error>(
                    Error.Validation("Invalid chain ID.", "IDENTITY.WALLET.CHAIN.INVALID"));
            }

            var addressResult = Address.CreateForChain(chainId, request.RawAddress);

            if (addressResult.IsFailure)
            {
                _logger.LogWarning("Invalid address format: {Address}", request.RawAddress);
                return Result.Failure<WalletDto, Error>(
                    Error.Validation("Invalid address format.", "IDENTITY.WALLET.COORDINATES.INVALID"));
            }

            var address = addressResult.Value;

            // Use specification to find wallet by coordinates
            var spec = new WalletByCoordinatesSpec(chainId, address);
            var wallet = await _walletRepository.FirstOrDefaultAsync(spec, cancellationToken);

            if (wallet == null)
            {
                _logger.LogInformation("Wallet not found for coordinates: {ChainId}, {Address}", 
                    request.ChainId, request.RawAddress);
                return Result.Failure<WalletDto, Error>(
                    Error.NotFound("Wallet not found.", "IDENTITY.WALLET.NOT_FOUND"));
            }

            // Check tag viewing permissions
            var canViewTags = await _authorizationService.CanViewTagsAsync(wallet.Id, cancellationToken);

            // Map to DTO with tag redaction
            var walletDto = IdentityDtoMapper.ToWalletDto(wallet, canViewTags);

            _logger.LogDebug("Retrieved wallet by coordinates {ChainId}, {Address}, tags included: {TagsIncluded}", 
                request.ChainId, request.RawAddress, canViewTags);

            return Result.Success<WalletDto, Error>(walletDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve wallet by coordinates {ChainId}, {Address}", 
                request.ChainId, request.RawAddress);
            return Result.Failure<WalletDto, Error>(
                Error.Internal("Failed to retrieve wallet.", "IDENTITY.WALLET.GET.FAILED"));
        }
    }
}