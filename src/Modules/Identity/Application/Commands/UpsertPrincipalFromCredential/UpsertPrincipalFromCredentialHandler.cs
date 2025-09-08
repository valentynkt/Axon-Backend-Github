using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.Common.Mappers;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;
using Axon.Modules.Identity.Application.Services;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Commands.UpsertPrincipalFromCredential;

/// <summary>
/// Handles UpsertPrincipalFromCredential command.
/// Resolves or creates a human principal from a credential and updates last-seen if found.
/// Wallet operations should be performed separately through dedicated wallet commands.
/// </summary>
public sealed class UpsertPrincipalFromCredentialHandler : BaseIdentityCommandHandler<UpsertPrincipalFromCredentialCommand, UpsertPrincipalResponse>
{
    private readonly IAxonPrincipalWriteRepository _principalRepository;
    private readonly IWalletOwnershipService _walletOwnershipService;
    private readonly ILogger<UpsertPrincipalFromCredentialHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public UpsertPrincipalFromCredentialHandler(
        ICurrentUserService currentUserService,
        IAxonPrincipalWriteRepository principalRepository,
        IWalletOwnershipService walletOwnershipService,
        TimeProvider timeProvider,
        ILogger<UpsertPrincipalFromCredentialHandler> logger)
        : base(currentUserService)
    {
        _principalRepository = principalRepository;
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

        await _principalRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success<UpsertPrincipalResponse, Error>(new UpsertPrincipalResponse(
            Principal: IdentityDtoMapper.ToPrincipalDto(principal),
            Credential: IdentityDtoMapper.ToCredentialDto(credential),
            Status: UpsertPrincipalStatus.UpdatedLastSeen
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
            command.EnvironmentId, timeProvider: _timeProvider);

        if (credentialResult.IsFailure)
        {
            return Result.Failure<UpsertPrincipalResponse, Error>(credentialResult.Error);
        }

        var credential = credentialResult.Value;

        // Add principal to repository (UnitOfWorkBehavior will handle SaveChanges)
        await _principalRepository.AddAsync(principal, cancellationToken);

        return Result.Success<UpsertPrincipalResponse, Error>(new UpsertPrincipalResponse(
            Principal: IdentityDtoMapper.ToPrincipalDto(principal),
            Credential: IdentityDtoMapper.ToCredentialDto(credential),
            Status: UpsertPrincipalStatus.Created
        ));
    }

}