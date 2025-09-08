using Axon.Modules.Identity.Application.Abstractions;
using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.Common.Mappers;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Commands.SignInWithWallet;

/// <summary>
/// Handles SignInWithWallet command for wallet-first authentication flow.
/// Atomically verifies wallet control and links/creates principal with verified signing proof.
/// </summary>
public sealed class SignInWithWalletHandler : BaseIdentityCommandHandler<SignInWithWalletCommand, SignInWithWalletResponse>
{
    private readonly IAxonPrincipalWriteRepository _principalRepository;
    private readonly IWalletResolutionService _walletResolutionService;
    private readonly IDefaultWalletCoordinator _defaultWalletCoordinator;
    private readonly ISignatureVerifier _signatureVerifier;
    private readonly IChallengeStore _challengeStore;
    private readonly ILogger<SignInWithWalletHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public SignInWithWalletHandler(
        ICurrentUserService currentUserService,
        IAxonPrincipalWriteRepository principalRepository,
        IWalletResolutionService walletResolutionService,
        IDefaultWalletCoordinator defaultWalletCoordinator,
        ISignatureVerifier signatureVerifier,
        IChallengeStore challengeStore,
        TimeProvider timeProvider,
        ILogger<SignInWithWalletHandler> logger)
        : base(currentUserService)
    {
        _principalRepository = principalRepository;
        _walletResolutionService = walletResolutionService;
        _defaultWalletCoordinator = defaultWalletCoordinator;
        _signatureVerifier = signatureVerifier;
        _challengeStore = challengeStore;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public override async Task<Result<SignInWithWalletResponse, Error>> Handle(
        SignInWithWalletCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing SignInWithWallet command for chain {ChainId} address {Address}",
            command.ChainId, command.RawAddress);

        // Parse and validate incoming address
        var addressResult = Address.CreateForChain(command.ChainId, command.RawAddress);
        if (addressResult.IsFailure)
        {
            return Result.Failure<SignInWithWalletResponse, Error>(addressResult.Error);
        }

        var incomingAddress = addressResult.Value;

        // Validate challenge and get bound information
        var challengeValidationResult = await _challengeStore.ValidateChallengeAsync(
            command.ChallengeId, cancellationToken);
        if (challengeValidationResult.IsFailure)
        {
            return Result.Failure<SignInWithWalletResponse, Error>(
                IdentityDomainErrors.Authentication.ChallengeInvalid());
        }

        var challengeInfo = challengeValidationResult.Value;

        // Verify challenge binding - canonical address must match
        if (challengeInfo.ChainId != command.ChainId || challengeInfo.CanonicalAddress != incomingAddress)
        {
            _logger.LogWarning("Challenge binding mismatch - Challenge bound to {BoundChain}:{BoundAddress}, but request for {RequestChain}:{RequestAddress}",
                challengeInfo.ChainId, challengeInfo.CanonicalAddress, command.ChainId, incomingAddress);
            return Result.Failure<SignInWithWalletResponse, Error>(
                IdentityDomainErrors.Authentication.ChallengeBindingMismatch());
        }

        // Verify signature using the challenge-bound canonical address
        var signatureVerificationResult = await _signatureVerifier.VerifySignatureAsync(
            challengeInfo.ChainId, challengeInfo.CanonicalAddress, command.Signature, command.ChallengeId, cancellationToken);
        if (signatureVerificationResult.IsFailure)
        {
            return Result.Failure<SignInWithWalletResponse, Error>(
                IdentityDomainErrors.Authentication.SignatureInvalid());
        }

        // Consume the challenge to prevent replay attacks
        await _challengeStore.ConsumeChallengeAsync(command.ChallengeId, cancellationToken);

        // Resolve or register wallet using service
        var walletResolutionResult = await _walletResolutionService.ResolveOrRegisterAsync(
            command.ChainId, command.RawAddress, cancellationToken);
        if (walletResolutionResult.IsFailure)
        {
            return Result.Failure<SignInWithWalletResponse, Error>(walletResolutionResult.Error);
        }

        var (wallet, _) = walletResolutionResult.Value;

        // Find existing owner of this wallet
        var existingOwner = await _principalRepository.FindByWalletIdAsync(wallet.Id, cancellationToken);

        if (existingOwner is not null)
        {
            // Existing principal owns this wallet - sign them in
            var ownership = existingOwner.WalletOwnerships
                .First(w => w.WalletId == wallet.Id && !w.IsDeleted);

            // Verify and update wallet ownership if needed
            if (!ownership.State.IsVerified)
            {
                var verifyResult = existingOwner.VerifyWalletOwnership(wallet.Id, timeProvider: _timeProvider);
                if (verifyResult.IsFailure)
                {
                    return Result.Failure<SignInWithWalletResponse, Error>(verifyResult.Error);
                }
            }

            // Handle default setting if requested
            bool? appliedDefault = null;
            string? defaultNotAppliedReason = null;

            if (command.SetAsDefault)
            {
                var (applied, reason) = await _defaultWalletCoordinator.ApplyIfEligibleAsync(
                    existingOwner, command.ChainId, wallet.Id, cancellationToken);
                
                appliedDefault = applied;
                defaultNotAppliedReason = reason;
            }

            await _principalRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success<SignInWithWalletResponse, Error>(new SignInWithWalletResponse(
                Principal: IdentityDtoMapper.ToPrincipalDto(existingOwner),
                WalletOwnership: IdentityDtoMapper.ToWalletOwnershipDto(ownership),
                AppliedDefault: appliedDefault,
                DefaultNotAppliedReason: defaultNotAppliedReason,
                Status: SignInWithWalletStatus.ExistingPrincipalFoundByWallet
            ));
        }

        // No existing owner - create new human principal
        var newPrincipalResult = AxonPrincipal.CreateHumanPrincipal(timeProvider: _timeProvider);
        if (newPrincipalResult.IsFailure)
        {
            return Result.Failure<SignInWithWalletResponse, Error>(newPrincipalResult.Error);
        }

        var newPrincipal = newPrincipalResult.Value;

        // Parse access mode (default to signing for sign-in)
        var accessMode = AccessMode.Default; // Default is signing
        if (!string.IsNullOrEmpty(command.AccessMode))
        {
            var accessModeResult = AccessMode.Create(command.AccessMode);
            if (accessModeResult.IsFailure)
            {
                return Result.Failure<SignInWithWalletResponse, Error>(accessModeResult.Error);
            }
            accessMode = accessModeResult.Value;
        }

        // Link wallet with verified signing proof (signature was already verified)
        var linkResult = newPrincipal.LinkWallet(
            wallet.Id,
            command.ChainId,
            ProofType.DirectSignature, // We verified the signature directly
            accessMode,
            command.Label,
            _timeProvider);

        if (linkResult.IsFailure)
        {
            return Result.Failure<SignInWithWalletResponse, Error>(linkResult.Error);
        }

        var newOwnership = linkResult.Value;

        // Verify the ownership (signature was already verified)
        var verifyNewOwnershipResult = newPrincipal.VerifyWalletOwnership(wallet.Id, timeProvider: _timeProvider);
        if (verifyNewOwnershipResult.IsFailure)
        {
            return Result.Failure<SignInWithWalletResponse, Error>(verifyNewOwnershipResult.Error);
        }

        // Handle default setting if requested
        bool? finalAppliedDefault = null;
        string? finalDefaultNotAppliedReason = null;

        if (command.SetAsDefault)
        {
            var (applied, reason) = await _defaultWalletCoordinator.ApplyIfEligibleAsync(
                newPrincipal, command.ChainId, wallet.Id, cancellationToken);
            
            finalAppliedDefault = applied;
            finalDefaultNotAppliedReason = reason;
        }

        // Save the new principal
        await _principalRepository.AddAsync(newPrincipal, cancellationToken);
        await _principalRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success<SignInWithWalletResponse, Error>(new SignInWithWalletResponse(
            Principal: IdentityDtoMapper.ToPrincipalDto(newPrincipal),
            WalletOwnership: IdentityDtoMapper.ToWalletOwnershipDto(newOwnership),
            AppliedDefault: finalAppliedDefault,
            DefaultNotAppliedReason: finalDefaultNotAppliedReason,
            Status: SignInWithWalletStatus.PrincipalCreatedAndWalletLinked
        ));
    }

}