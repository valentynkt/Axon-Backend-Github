using Axon.Modules.Identity.Application.Common.Mappers;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Application.Specifications.AxonPrincipals;
using Axon.Modules.Identity.Application.Specifications.Wallets;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Queries.GetMyWalletOwnership;

/// <summary>
/// Handler for checking wallet ownership by the authenticated caller.
/// Returns ownership details or NOT_OWNED status.
/// </summary>
public sealed class GetMyWalletOwnershipQueryHandler : IRequestHandler<GetMyWalletOwnershipQuery, Result<WalletOwnershipStatusDto, Error>>
{
    private readonly IAxonPrincipalReadRepository _principalRepository;
    private readonly IWalletReadRepository _walletRepository;
    private readonly IWalletAuthorizationService _authorizationService;
    private readonly ILogger<GetMyWalletOwnershipQueryHandler> _logger;

    public GetMyWalletOwnershipQueryHandler(
        IAxonPrincipalReadRepository principalRepository,
        IWalletReadRepository walletRepository,
        IWalletAuthorizationService authorizationService,
        ILogger<GetMyWalletOwnershipQueryHandler> logger)
    {
        _principalRepository = principalRepository ?? throw new ArgumentNullException(nameof(principalRepository));
        _walletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<WalletOwnershipStatusDto, Error>> Handle(
        GetMyWalletOwnershipQuery request, 
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Get current user's AxonId
            var currentAxonId = await _authorizationService.GetCurrentUserAxonIdAsync(cancellationToken);
            if (currentAxonId == null)
            {
                return Result.Failure<WalletOwnershipStatusDto, Error>(
                    Error.Unauthorized("User is not authenticated.", "AUTH.NOT_AUTHENTICATED"));
            }

            WalletId? walletId = null;

            // Handle query by wallet ID
            if (!string.IsNullOrEmpty(request.WalletId))
            {
                if (!Guid.TryParse(request.WalletId, out var walletGuid))
                {
                    return Result.Failure<WalletOwnershipStatusDto, Error>(
                        Error.Validation("Invalid wallet ID format.", "IDENTITY.WALLET.ID.INVALID"));
                }
                walletId = new WalletId(walletGuid);
            }
            // Handle query by coordinates
            else if (!string.IsNullOrEmpty(request.ChainId) && !string.IsNullOrEmpty(request.RawAddress))
            {
                ChainId chainId;
                try
                {
                    chainId = ChainId.From(request.ChainId);
                }
                catch (Exception)
                {
                    _logger.LogWarning("Invalid chain ID format: {ChainId}", request.ChainId);
                    return Result.Failure<WalletOwnershipStatusDto, Error>(
                        Error.Validation("Invalid chain ID.", "IDENTITY.WALLET.CHAIN.INVALID"));
                }

                var addressResult = Address.CreateForChain(chainId, request.RawAddress);

                if (addressResult.IsFailure)
                {
                    return Result.Failure<WalletOwnershipStatusDto, Error>(
                        Error.Validation("Invalid address format.", "IDENTITY.WALLET.COORDINATES.INVALID"));
                }

                // Find wallet by coordinates first
                var walletSpec = new WalletByCoordinatesSpec(chainId, addressResult.Value);
                var wallet = await _walletRepository.FirstOrDefaultAsync(walletSpec, cancellationToken);
                
                if (wallet == null)
                {
                    _logger.LogDebug("Wallet not found for ownership check: {ChainId}, {Address} - returning NOT_OWNED to prevent enumeration", 
                        request.ChainId, request.RawAddress);
                    return Result.Success<WalletOwnershipStatusDto, Error>(
                        WalletOwnershipStatusDto.NotOwned());
                }

                walletId = wallet.Id;
            }

            if (walletId == null)
            {
                return Result.Failure<WalletOwnershipStatusDto, Error>(
                    Error.Validation("Invalid query parameters.", "IDENTITY.WALLET.OWNERSHIP.INVALID_INPUT"));
            }

            // Check if current user owns this wallet
            var ownershipSpec = new CallerWalletOwnershipSpec(currentAxonId.Value, walletId.Value);
            var principal = await _principalRepository.FirstOrDefaultAsync(ownershipSpec, cancellationToken);

            if (principal == null)
            {
                // User does not own this wallet
                _logger.LogDebug("Wallet ownership check: NOT_OWNED for user {AxonId}, wallet {WalletId}", 
                    currentAxonId, walletId);
                return Result.Success<WalletOwnershipStatusDto, Error>(
                    WalletOwnershipStatusDto.NotOwned());
            }

            // Find the specific ownership
            var ownership = principal.WalletOwnerships
                .FirstOrDefault(wo => wo.WalletId == walletId && !wo.IsDeleted);

            if (ownership == null)
            {
                return Result.Success<WalletOwnershipStatusDto, Error>(
                    WalletOwnershipStatusDto.NotOwned());
            }

            // Map ownership to DTO and return
            var ownershipDto = IdentityDtoMapper.ToWalletOwnershipDto(ownership);
            
            _logger.LogDebug("Wallet ownership check: OWNED for user {AxonId}, wallet {WalletId}", 
                currentAxonId, walletId);

            return Result.Success<WalletOwnershipStatusDto, Error>(
                WalletOwnershipStatusDto.Owned(ownershipDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check wallet ownership for user");
            return Result.Failure<WalletOwnershipStatusDto, Error>(
                Error.Internal("Failed to check wallet ownership.", "IDENTITY.WALLET.OWNERSHIP.FAILED"));
        }
    }
}