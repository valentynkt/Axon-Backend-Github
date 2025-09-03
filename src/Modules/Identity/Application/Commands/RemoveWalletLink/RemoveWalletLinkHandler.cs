using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;
using Axon.Modules.Identity.Domain.Errors;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Commands.RemoveWalletLink;

/// <summary>
/// Handles RemoveWalletLink command.
/// Unlinks ownership from a principal and clears any chain default that pointed at it.
/// </summary>
public sealed class RemoveWalletLinkHandler : BaseIdentityCommandHandler<RemoveWalletLinkCommand, RemoveWalletResponse>
{
    private readonly IAxonPrincipalWriteRepository _principalRepository;
    private readonly ILogger<RemoveWalletLinkHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public RemoveWalletLinkHandler(
        ICurrentUserService currentUserService,
        IAxonPrincipalWriteRepository principalRepository,
        TimeProvider timeProvider,
        ILogger<RemoveWalletLinkHandler> logger)
        : base(currentUserService)
    {
        _principalRepository = principalRepository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public override async Task<Result<RemoveWalletResponse, Error>> Handle(
        RemoveWalletLinkCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing RemoveWalletLink command for principal {PrincipalId} and wallet {WalletId}", 
            command.AxonId, command.WalletId);

        // Get the principal
        var principal = await _principalRepository.GetByIdAsync(command.AxonId, cancellationToken);
        if (principal is null)
        {
            return Result.Failure<RemoveWalletResponse, Error>(
                IdentityDomainErrors.Principal.NotFound());
        }

        // Check if the principal owns the wallet
        var ownership = principal.WalletOwnerships
            .FirstOrDefault(w => w.WalletId == command.WalletId && !w.IsDeleted);

        if (ownership is null)
        {
            // Principal doesn't own this wallet
            return Result.Success<RemoveWalletResponse, Error>(new RemoveWalletResponse(
                AxonId: command.AxonId,
                WalletId: command.WalletId,
                Status: RemoveWalletStatus.NotOwned
            ));
        }

        // Unlink the wallet (this also clears chain defaults)
        var unlinkResult = principal.UnlinkWallet(command.WalletId, _timeProvider);
        if (unlinkResult.IsFailure)
        {
            return Result.Failure<RemoveWalletResponse, Error>(unlinkResult.Error);
        }

        // Save changes
        await _principalRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success<RemoveWalletResponse, Error>(new RemoveWalletResponse(
            AxonId: command.AxonId,
            WalletId: command.WalletId,
            Status: RemoveWalletStatus.Unlinked
        ));
    }
}