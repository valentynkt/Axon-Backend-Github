using Axon.Modules.Identity.Application.Common.Mappers;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Application.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Queries.GetWallet;

/// <summary>
/// Handler for retrieving a wallet by ID with appropriate authorization and tag redaction.
/// </summary>
public sealed class GetWalletQueryHandler : IRequestHandler<GetWalletQuery, Result<WalletDto, Error>>
{
    private readonly IWalletReadRepository _walletRepository;
    private readonly IWalletAuthorizationService _authorizationService;
    private readonly ILogger<GetWalletQueryHandler> _logger;

    public GetWalletQueryHandler(
        IWalletReadRepository walletRepository,
        IWalletAuthorizationService authorizationService,
        ILogger<GetWalletQueryHandler> logger)
    {
        _walletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<WalletDto, Error>> Handle(GetWalletQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Parse wallet ID
            if (!Guid.TryParse(request.WalletId, out var walletGuid))
            {
                return Result.Failure<WalletDto, Error>(
                    Error.Validation("Invalid wallet ID format.", "IDENTITY.WALLET.ID.INVALID"));
            }

            var walletId = new WalletId(walletGuid);

            // Retrieve wallet (excluding soft-deleted)
            var wallet = await _walletRepository.GetByIdAsync(walletId, cancellationToken);
            if (wallet == null || wallet.IsDeleted)
            {
                _logger.LogInformation("Wallet not found: {WalletId}", request.WalletId);
                return Result.Failure<WalletDto, Error>(
                    Error.NotFound("Wallet not found.", "IDENTITY.WALLET.NOT_FOUND"));
            }

            // Check tag viewing permissions
            var canViewTags = await _authorizationService.CanViewTagsAsync(walletId, cancellationToken);

            // Map to DTO with tag redaction
            var walletDto = IdentityDtoMapper.ToWalletDto(wallet, canViewTags);

            _logger.LogDebug("Retrieved wallet {WalletId}, tags included: {TagsIncluded}", 
                request.WalletId, canViewTags);

            return Result.Success<WalletDto, Error>(walletDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve wallet {WalletId}", request.WalletId);
            return Result.Failure<WalletDto, Error>(
                Error.Internal("Failed to retrieve wallet.", "IDENTITY.WALLET.GET.FAILED"));
        }
    }
}