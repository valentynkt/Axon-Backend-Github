using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Commands.RevokeCredential;

/// <summary>
/// Handles RevokeCredential command.
/// Soft deletes a credential identified by one of two paths:
/// either by CredentialId or by ProviderType + Issuer + Subject combination.
/// </summary>
public sealed class RevokeCredentialHandler : BaseIdentityCommandHandler<RevokeCredentialCommand, RevokeCredentialResponse>
{
    private readonly IAxonPrincipalWriteRepository _principalRepository;
    private readonly ILogger<RevokeCredentialHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public RevokeCredentialHandler(
        ICurrentUserService currentUserService,
        IAxonPrincipalWriteRepository principalRepository,
        TimeProvider timeProvider,
        ILogger<RevokeCredentialHandler> logger)
        : base(currentUserService)
    {
        _principalRepository = principalRepository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public override async Task<Result<RevokeCredentialResponse, Error>> Handle(
        RevokeCredentialCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing RevokeCredential command for principal {PrincipalId}", command.AxonId);

        // Validate credential identifier
        if (!command.CredentialIdentifier.IsValid())
        {
            return Result.Failure<RevokeCredentialResponse, Error>(
                IdentityDomainErrors.Validation.EitherCredentialIdOrIdentifiersRequired());
        }

        // Get the principal
        var principal = await _principalRepository.GetByIdAsync(command.AxonId, cancellationToken);
        if (principal is null)
        {
            return Result.Failure<RevokeCredentialResponse, Error>(
                IdentityDomainErrors.Principal.NotFound());
        }

        // Find the credential
        var credentialResult = FindCredential(principal, command.CredentialIdentifier);
        if (credentialResult.IsFailure)
        {
            return Result.Failure<RevokeCredentialResponse, Error>(credentialResult.Error);
        }

        var credential = credentialResult.Value;

        // Check if credential is already revoked
        if (credential.IsDeleted)
        {
            return Result.Success<RevokeCredentialResponse, Error>(new RevokeCredentialResponse(
                AxonId: command.AxonId,
                CredentialId: credential.Id,
                Status: RevokeCredentialStatus.AlreadyRevoked
            ));
        }

        // Revoke the credential using domain method
        var providerType = credential.ProviderType;
        var issuer = credential.Issuer;
        var subject = credential.Subject;

        var revokeResult = principal.RevokeCredential(
            providerType, issuer, subject, command.Reason, _timeProvider);

        if (revokeResult.IsFailure)
        {
            return Result.Failure<RevokeCredentialResponse, Error>(revokeResult.Error);
        }

        // Save changes
        await _principalRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success<RevokeCredentialResponse, Error>(new RevokeCredentialResponse(
            AxonId: command.AxonId,
            CredentialId: credential.Id,
            Status: RevokeCredentialStatus.Revoked
        ));
    }

    private static Result<IdentityCredential, Error> FindCredential(
        Domain.Aggregates.AxonPrincipal.AxonPrincipal principal,
        DTOs.Requests.CredentialIdentifier identifier)
    {
        // Try by CredentialId first
        if (identifier.CredentialId.HasValue)
        {
            var credential = principal.Credentials
                .FirstOrDefault(c => c.Id == identifier.CredentialId.Value);

            if (credential is null)
            {
                return Result.Failure<IdentityCredential, Error>(
                    IdentityDomainErrors.Credential.NotFound());
            }

            return Result.Success<IdentityCredential, Error>(credential);
        }

        // Try by provider details
        if (identifier.ProviderType is not null && 
            identifier.Issuer is not null && 
            identifier.Subject is not null)
        {
            var providerTypeResult = ProviderType.Create(identifier.ProviderType);
            if (providerTypeResult.IsFailure)
            {
                return Result.Failure<IdentityCredential, Error>(providerTypeResult.Error);
            }

            var credential = principal.Credentials
                .FirstOrDefault(c => 
                    c.ProviderType == providerTypeResult.Value &&
                    c.Issuer == identifier.Issuer &&
                    c.Subject == identifier.Subject);

            if (credential is null)
            {
                return Result.Failure<IdentityCredential, Error>(
                    IdentityDomainErrors.Credential.NotFound());
            }

            return Result.Success<IdentityCredential, Error>(credential);
        }

        return Result.Failure<IdentityCredential, Error>(
            IdentityDomainErrors.Validation.InvalidCredentialIdentifier());
    }
}