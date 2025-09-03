using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.Common.Mappers;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Commands.EnsureWalletLinked;

/// <summary>
/// Handles EnsureWalletLinked command.
/// Given a principal and wallet coordinates, ensures the wallet exists and is linked 
/// with the desired state (verify, default, label, access mode).
/// </summary>
public sealed class EnsureWalletLinkedHandler : BaseIdentityIdempotentCommandHandler<EnsureWalletLinkedCommand, EnsureWalletResponse>
{
    private readonly IAxonPrincipalWriteRepository _principalRepository;
    private readonly IWalletResolutionService _walletResolutionService;
    private readonly IDefaultWalletCoordinator _defaultWalletCoordinator;
    private readonly IWalletOwnershipService _walletOwnershipService;
    private readonly ILogger<EnsureWalletLinkedHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public EnsureWalletLinkedHandler(
        ICurrentUserService currentUserService,
        IAxonPrincipalWriteRepository principalRepository,
        IWalletResolutionService walletResolutionService,
        IDefaultWalletCoordinator defaultWalletCoordinator,
        IWalletOwnershipService walletOwnershipService,
        TimeProvider timeProvider,
        ILogger<EnsureWalletLinkedHandler> logger)
        : base(currentUserService)
    {
        _principalRepository = principalRepository;
        _walletResolutionService = walletResolutionService;
        _defaultWalletCoordinator = defaultWalletCoordinator;
        _walletOwnershipService = walletOwnershipService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public override async Task<Result<EnsureWalletResponse, Error>> Handle(
        EnsureWalletLinkedCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing EnsureWalletLinked command for principal {PrincipalId}", command.AxonId);

        // Get the principal
        var principal = await _principalRepository.GetByIdAsync(command.AxonId, cancellationToken);
        if (principal is null)
        {
            return Result.Failure<EnsureWalletResponse, Error>(
                IdentityDomainErrors.Principal.NotFound());
        }

        // Parse address
        var addressResult = Address.CreateForChain(command.ChainId, command.RawAddress);
        if (addressResult.IsFailure)
        {
            return Result.Failure<EnsureWalletResponse, Error>(addressResult.Error);
        }

        // Parse proof type
        var proofTypeResult = ProofType.Create(command.ProofType);
        if (proofTypeResult.IsFailure)
        {
            return Result.Failure<EnsureWalletResponse, Error>(proofTypeResult.Error);
        }

        // Parse access mode if provided
        AccessMode? accessMode = null;
        if (!string.IsNullOrEmpty(command.AccessMode))
        {
            var accessModeResult = AccessMode.Create(command.AccessMode);
            if (accessModeResult.IsFailure)
            {
                return Result.Failure<EnsureWalletResponse, Error>(accessModeResult.Error);
            }
            accessMode = accessModeResult.Value;
        }

        // Resolve or register wallet using service
        var walletResolutionResult = await _walletResolutionService.ResolveOrRegisterAsync(
            command.ChainId, command.RawAddress, cancellationToken);
        if (walletResolutionResult.IsFailure)
        {
            return Result.Failure<EnsureWalletResponse, Error>(walletResolutionResult.Error);
        }

        var (wallet, walletWasCreated) = walletResolutionResult.Value;

        // Check for ownership conflicts using the service
        var conflictCheckResult = await _walletOwnershipService.CheckWalletOwnershipConflictAsync(
            wallet.Id, command.AxonId, proofTypeResult.Value, cancellationToken);

        if (conflictCheckResult.IsFailure)
        {
            return Result.Failure<EnsureWalletResponse, Error>(conflictCheckResult.Error);
        }

        // Handle structured conflict result
        if (conflictCheckResult.Value is Conflict conflict)
        {
            return Result.Success<EnsureWalletResponse, Error>(new EnsureWalletResponse(
                Wallet: MapToWalletDto(wallet),
                Ownership: null,
                AppliedDefault: false,
                DefaultNotAppliedReason: $"Wallet is owned by another principal: {conflict.ExistingOwnerPrincipalId}",
                Status: EnsureWalletStatus.ConflictOwnedByOther
            ));
        }

        // Check if principal already owns this wallet
        var existingOwnership = principal.WalletOwnerships
            .FirstOrDefault(w => w.WalletId == wallet.Id && !w.IsDeleted);

        if (existingOwnership is not null)
        {
            // Wallet is already linked - check if we need to update desired state
            var hasChanges = false;

            // Handle label update if provided and different from current
            if (!string.IsNullOrEmpty(command.Label) && existingOwnership.Label != command.Label)
            {
                var updateLabelResult = principal.UpdateWalletLabel(wallet.Id, command.Label, _timeProvider);
                if (updateLabelResult.IsFailure)
                {
                    return Result.Failure<EnsureWalletResponse, Error>(updateLabelResult.Error);
                }
                hasChanges = true;
            }

            // Handle access mode update if provided and different from current
            if (accessMode is not null && existingOwnership.AccessMode != accessMode.Value)
            {
                var updateAccessModeResult = principal.UpdateWalletAccessMode(wallet.Id, accessMode.Value, _timeProvider);
                if (updateAccessModeResult.IsFailure)
                {
                    return Result.Failure<EnsureWalletResponse, Error>(updateAccessModeResult.Error);
                }
                hasChanges = true;
            }

            // Handle verification if requested and not already verified
            if (command.Verify && proofTypeResult.Value.SupportsVerification && !existingOwnership.State.IsVerified)
            {
                var verifyResult = principal.VerifyWalletOwnership(wallet.Id, timeProvider: _timeProvider);
                if (verifyResult.IsFailure)
                {
                    return Result.Failure<EnsureWalletResponse, Error>(verifyResult.Error);
                }
                hasChanges = true;
            }

            // Handle default setting
            bool? appliedDefault = null;
            string? defaultNotAppliedReason = null;

            if (command.SetAsDefault)
            {
                var (applied, reason) = await _defaultWalletCoordinator.ApplyIfEligibleAsync(
                    principal, command.ChainId, wallet.Id, cancellationToken);
                
                appliedDefault = applied;
                defaultNotAppliedReason = reason;
                
                if (applied == true)
                {
                    hasChanges = true;
                }
            }

            // Save changes if any
            if (hasChanges)
            {
                await _principalRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            }

            var status = hasChanges ? EnsureWalletStatus.Updated : EnsureWalletStatus.AlreadyLinked;

            return Result.Success<EnsureWalletResponse, Error>(new EnsureWalletResponse(
                Wallet: MapToWalletDto(wallet),
                Ownership: MapToWalletOwnershipDto(existingOwnership),
                AppliedDefault: appliedDefault,
                DefaultNotAppliedReason: defaultNotAppliedReason,
                Status: status
            ));
        }

        // Link the wallet to the principal
        var linkResult = principal.LinkWallet(
            wallet.Id, command.ChainId, proofTypeResult.Value, 
            accessMode, command.Label, _timeProvider);

        if (linkResult.IsFailure)
        {
            return Result.Failure<EnsureWalletResponse, Error>(linkResult.Error);
        }

        var ownership = linkResult.Value;

        // Handle verification if requested
        if (command.Verify && proofTypeResult.Value.SupportsVerification)
        {
            var verifyResult = principal.VerifyWalletOwnership(wallet.Id, timeProvider: _timeProvider);
            if (verifyResult.IsFailure)
            {
                return Result.Failure<EnsureWalletResponse, Error>(verifyResult.Error);
            }
        }

        // Handle default setting
        bool? finalAppliedDefault = null;
        string? finalDefaultNotAppliedReason = null;

        if (command.SetAsDefault)
        {
            var (applied, reason) = await _defaultWalletCoordinator.ApplyIfEligibleAsync(
                principal, command.ChainId, wallet.Id, cancellationToken);
            
            finalAppliedDefault = applied;
            finalDefaultNotAppliedReason = reason;
        }

        // Save all changes
        await _principalRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        var finalStatus = walletWasCreated 
            ? EnsureWalletStatus.RegisteredAndLinked 
            : EnsureWalletStatus.Linked;

        return Result.Success<EnsureWalletResponse, Error>(new EnsureWalletResponse(
            Wallet: MapToWalletDto(wallet),
            Ownership: MapToWalletOwnershipDto(ownership),
            AppliedDefault: finalAppliedDefault,
            DefaultNotAppliedReason: finalDefaultNotAppliedReason,
            Status: finalStatus
        ));
    }

    // Mapping methods
    private static WalletDto MapToWalletDto(Wallet wallet)
    {
        return IdentityDtoMapper.ToWalletDto(wallet, includeTags: true);
    }

    private static WalletOwnershipDto MapToWalletOwnershipDto(WalletOwnership ownership)
    {
        return IdentityDtoMapper.ToWalletOwnershipDto(ownership);
    }
}