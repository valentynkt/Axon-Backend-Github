using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.Common.Mappers;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Commands.UpsertPrincipalFromCredential;

/// <summary>
/// Result of attaching wallet to principal with conflict information.
/// </summary>
public abstract record AttachWalletResult;

/// <summary>
/// Wallet successfully attached.
/// </summary>
public sealed record AttachWalletSuccess(
    WalletOwnership Ownership,
    bool? AppliedDefault,
    string? DefaultNotAppliedReason
) : AttachWalletResult;

/// <summary>
/// Wallet attachment failed due to ownership conflict.
/// </summary>
public sealed record AttachWalletConflict(
    AxonId ExistingOwnerPrincipalId
) : AttachWalletResult;

/// <summary>
/// Handles UpsertPrincipalFromCredential command.
/// Resolves or creates a human principal from a credential, updates last-seen if found,
/// optionally attaches and (optionally) verifies a wallet in one shot.
/// </summary>
public sealed class UpsertPrincipalFromCredentialHandler : BaseIdentityIdempotentCommandHandler<UpsertPrincipalFromCredentialCommand, UpsertPrincipalResponse>
{
    private readonly IAxonPrincipalWriteRepository _principalRepository;
    private readonly IWalletResolutionService _walletResolutionService;
    private readonly IDefaultWalletCoordinator _defaultWalletCoordinator;
    private readonly IWalletOwnershipService _walletOwnershipService;
    private readonly ILogger<UpsertPrincipalFromCredentialHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public UpsertPrincipalFromCredentialHandler(
        ICurrentUserService currentUserService,
        IAxonPrincipalWriteRepository principalRepository,
        IWalletResolutionService walletResolutionService,
        IDefaultWalletCoordinator defaultWalletCoordinator,
        IWalletOwnershipService walletOwnershipService,
        TimeProvider timeProvider,
        ILogger<UpsertPrincipalFromCredentialHandler> logger)
        : base(currentUserService)
    {
        _principalRepository = principalRepository;
        _walletResolutionService = walletResolutionService;
        _defaultWalletCoordinator = defaultWalletCoordinator;
        _walletOwnershipService = walletOwnershipService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public override async Task<Result<UpsertPrincipalResponse, Error>> Handle(
        UpsertPrincipalFromCredentialCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing UpsertPrincipalFromCredential command");

        // Validate provider type - reject service_api for this flow
        if (command.ProviderType == "service_api")
        {
            return Result.Failure<UpsertPrincipalResponse, Error>(
                IdentityDomainErrors.Credential.ProviderInvalidForContext(command.ProviderType, "UpsertPrincipalFromCredential"));
        }

        var providerType = ProviderType.Create(command.ProviderType);
        if (providerType.IsFailure)
        {
            return Result.Failure<UpsertPrincipalResponse, Error>(providerType.Error);
        }

        // Check if credential is already taken by another principal using service
        var credentialCheckResult = await _walletOwnershipService.CheckCredentialUniquenessAsync(
            providerType.Value, command.Issuer, command.Subject, cancellationToken);

        if (credentialCheckResult.IsFailure)
        {
            return Result.Failure<UpsertPrincipalResponse, Error>(credentialCheckResult.Error);
        }

        var existingPrincipal = credentialCheckResult.Value;
        if (existingPrincipal is not null)
        {
            return await HandleExistingPrincipal(existingPrincipal, command, cancellationToken);
        }

        // Create new principal
        return await CreateNewPrincipal(command, providerType.Value, cancellationToken);
    }

    private async Task<Result<UpsertPrincipalResponse, Error>> HandleExistingPrincipal(
        AxonPrincipal principal, 
        UpsertPrincipalFromCredentialCommand command,
        CancellationToken cancellationToken)
    {
        var providerType = ProviderType.Create(command.ProviderType);
        if (providerType.IsFailure)
        {
            return Result.Failure<UpsertPrincipalResponse, Error>(providerType.Error);
        }
        
        // Update credential last seen
        var updateResult = principal.UpdateCredentialLastSeen(
            providerType.Value, command.Issuer, command.Subject, _timeProvider);

        if (updateResult.IsFailure)
        {
            return Result.Failure<UpsertPrincipalResponse, Error>(updateResult.Error);
        }

        var credential = principal.Credentials.First(c => 
            c.ProviderType == providerType.Value && c.Issuer == command.Issuer && c.Subject == command.Subject);

        // Handle optional wallet attachment
        if (command.AttachWallet is not null)
        {
            var attachResult = await AttachWalletToPrincipal(principal, command.AttachWallet, cancellationToken);
            if (attachResult.IsFailure)
            {
                return Result.Failure<UpsertPrincipalResponse, Error>(attachResult.Error);
            }

            await _principalRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            return attachResult.Value switch
            {
                AttachWalletSuccess success => Result.Success<UpsertPrincipalResponse, Error>(new UpsertPrincipalResponse(
                    Principal: IdentityDtoMapper.ToPrincipalDto(principal),
                    Credential: IdentityDtoMapper.ToCredentialDto(credential),
                    AttachedWallet: IdentityDtoMapper.ToWalletOwnershipDto(success.Ownership),
                    AppliedDefault: success.AppliedDefault,
                    DefaultNotAppliedReason: success.DefaultNotAppliedReason,
                    Status: UpsertPrincipalStatus.LinkedWithWallet,
                    Conflict: null
                )),
                
                AttachWalletConflict conflict => Result.Success<UpsertPrincipalResponse, Error>(new UpsertPrincipalResponse(
                    Principal: IdentityDtoMapper.ToPrincipalDto(principal),
                    Credential: IdentityDtoMapper.ToCredentialDto(credential),
                    AttachedWallet: null,
                    AppliedDefault: null,
                    DefaultNotAppliedReason: null,
                    Status: UpsertPrincipalStatus.ConflictOwnedByOther,
                    Conflict: new ConflictInfo(conflict.ExistingOwnerPrincipalId)
                )),
                
                _ => throw new InvalidOperationException("Unknown AttachWalletResult type")
            };
        }

        await _principalRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success<UpsertPrincipalResponse, Error>(new UpsertPrincipalResponse(
            Principal: IdentityDtoMapper.ToPrincipalDto(principal),
            Credential: IdentityDtoMapper.ToCredentialDto(credential),
            AttachedWallet: null,
            AppliedDefault: null,
            DefaultNotAppliedReason: null,
            Status: UpsertPrincipalStatus.UpdatedLastSeen,
            Conflict: null
        ));
    }

    private async Task<Result<UpsertPrincipalResponse, Error>> CreateNewPrincipal(
        UpsertPrincipalFromCredentialCommand command,
        ProviderType providerType,
        CancellationToken cancellationToken)
    {
        // Parse email hash if provided
        EmailHash? emailHash = null;
        if (!string.IsNullOrEmpty(command.PrimaryEmailHash))
        {
            var emailHashResult = EmailHash.Create(command.PrimaryEmailHash);
            if (emailHashResult.IsFailure)
            {
                return Result.Failure<UpsertPrincipalResponse, Error>(emailHashResult.Error);
            }
            emailHash = emailHashResult.Value;
        }

        // Create principal
        var principalResult = AxonPrincipal.CreateHumanPrincipal(emailHash, _timeProvider);
        if (principalResult.IsFailure)
        {
            return Result.Failure<UpsertPrincipalResponse, Error>(principalResult.Error);
        }

        var principal = principalResult.Value;

        // Link credential
        var credentialResult = principal.LinkIdentityCredential(
            providerType, command.Issuer, command.Subject, 
            command.EnvironmentId, command.CredentialMetadata, _timeProvider);

        if (credentialResult.IsFailure)
        {
            return Result.Failure<UpsertPrincipalResponse, Error>(credentialResult.Error);
        }

        var credential = credentialResult.Value;

        // Handle optional wallet attachment
        WalletOwnership? walletOwnership = null;
        bool? appliedDefault = null;
        string? defaultNotAppliedReason = null;

        if (command.AttachWallet is not null)
        {
            var attachResult = await AttachWalletToPrincipal(principal, command.AttachWallet, cancellationToken);
            if (attachResult.IsFailure)
            {
                return Result.Failure<UpsertPrincipalResponse, Error>(attachResult.Error);
            }

            switch (attachResult.Value)
            {
                case AttachWalletSuccess success:
                    walletOwnership = success.Ownership;
                    appliedDefault = success.AppliedDefault;
                    defaultNotAppliedReason = success.DefaultNotAppliedReason;
                    break;
                    
                case AttachWalletConflict conflict:
                    // Save principal first, then return conflict
                    await _principalRepository.AddAsync(principal, cancellationToken);
                    await _principalRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
                    
                    return Result.Success<UpsertPrincipalResponse, Error>(new UpsertPrincipalResponse(
                        Principal: IdentityDtoMapper.ToPrincipalDto(principal),
                        Credential: IdentityDtoMapper.ToCredentialDto(credential),
                        AttachedWallet: null,
                        AppliedDefault: null,
                        DefaultNotAppliedReason: null,
                        Status: UpsertPrincipalStatus.ConflictOwnedByOther,
                        Conflict: new ConflictInfo(conflict.ExistingOwnerPrincipalId)
                    ));
                    
                default:
                    throw new InvalidOperationException("Unknown AttachWalletResult type");
            }
        }

        // Save principal
        await _principalRepository.AddAsync(principal, cancellationToken);
        await _principalRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        var status = walletOwnership is not null 
            ? UpsertPrincipalStatus.LinkedWithWallet 
            : UpsertPrincipalStatus.Created;

        return Result.Success<UpsertPrincipalResponse, Error>(new UpsertPrincipalResponse(
            Principal: IdentityDtoMapper.ToPrincipalDto(principal),
            Credential: IdentityDtoMapper.ToCredentialDto(credential),
            AttachedWallet: walletOwnership is not null ? IdentityDtoMapper.ToWalletOwnershipDto(walletOwnership) : null,
            AppliedDefault: appliedDefault,
            DefaultNotAppliedReason: defaultNotAppliedReason,
            Status: status,
            Conflict: null
        ));
    }

    private async Task<Result<AttachWalletResult, Error>> AttachWalletToPrincipal(
        AxonPrincipal principal,
        DTOs.Requests.AttachWalletRequest attachRequest,
        CancellationToken cancellationToken)
    {
        // Use the already-parsed ChainId directly
        var chainId = attachRequest.ChainId;

        // Parse address
        var addressResult = Address.CreateForChain(chainId, attachRequest.RawAddress);
        if (addressResult.IsFailure)
        {
            return Result.Failure<AttachWalletResult, Error>(addressResult.Error);
        }

        // Resolve or register wallet using service
        var walletResolutionResult = await _walletResolutionService.ResolveOrRegisterAsync(
            chainId, attachRequest.RawAddress, cancellationToken);
        if (walletResolutionResult.IsFailure)
        {
            return Result.Failure<AttachWalletResult, Error>(walletResolutionResult.Error);
        }

        var (wallet, _) = walletResolutionResult.Value;

        // Parse proof type
        var proofTypeResult = ProofType.Create(attachRequest.ProofType);
        if (proofTypeResult.IsFailure)
        {
            return Result.Failure<AttachWalletResult, Error>(proofTypeResult.Error);
        }

        // Parse access mode
        AccessMode? accessMode = null;
        if (!string.IsNullOrEmpty(attachRequest.AccessMode))
        {
            var accessModeResult = AccessMode.Create(attachRequest.AccessMode);
            if (accessModeResult.IsFailure)
            {
                return Result.Failure<AttachWalletResult, Error>(accessModeResult.Error);
            }
            accessMode = accessModeResult.Value;
        }

        // Check for existing ownership conflicts using service
        var conflictCheckResult = await _walletOwnershipService.CheckWalletOwnershipConflictAsync(
            wallet.Id, principal.Id, proofTypeResult.Value, cancellationToken);

        if (conflictCheckResult.IsFailure)
        {
            return Result.Failure<AttachWalletResult, Error>(conflictCheckResult.Error);
        }

        // Handle structured conflict result properly
        if (conflictCheckResult.Value is Conflict conflict)
        {
            return Result.Success<AttachWalletResult, Error>(
                new AttachWalletConflict(conflict.ExistingOwnerPrincipalId));
        }

        // Link wallet to principal
        var linkResult = principal.LinkWallet(
            wallet.Id, chainId, proofTypeResult.Value, 
            accessMode, attachRequest.Label, _timeProvider);

        if (linkResult.IsFailure)
        {
            return Result.Failure<AttachWalletResult, Error>(linkResult.Error);
        }

        var ownership = linkResult.Value;

        // Handle verification if requested
        if (attachRequest.Verify && proofTypeResult.Value.SupportsVerification)
        {
            var verifyResult = principal.VerifyWalletOwnership(wallet.Id, timeProvider: _timeProvider);
            if (verifyResult.IsFailure)
            {
                return Result.Failure<AttachWalletResult, Error>(verifyResult.Error);
            }
        }

        // Handle default setting
        bool? appliedDefault = null;
        string? defaultNotAppliedReason = null;

        if (attachRequest.SetAsDefault)
        {
            var (applied, reason) = await _defaultWalletCoordinator.ApplyIfEligibleAsync(
                principal, chainId, wallet.Id, cancellationToken);
            
            appliedDefault = applied;
            defaultNotAppliedReason = reason;
        }

        return Result.Success<AttachWalletResult, Error>(
            new AttachWalletSuccess(ownership, appliedDefault, defaultNotAppliedReason));
    }


}