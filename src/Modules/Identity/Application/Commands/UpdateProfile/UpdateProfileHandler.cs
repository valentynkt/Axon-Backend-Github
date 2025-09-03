using Axon.Modules.Identity.Application.Common.Commands;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Responses.CommandResponses;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Commands.UpdateProfile;

/// <summary>
/// Handles UpdateProfile command.
/// Updates language and/or risk tier in one call for a specific principal.
/// Supports partial updates and enforces service principal constraints.
/// </summary>
public sealed class UpdateProfileHandler : BaseIdentityCommandHandler<UpdateProfileCommand, UpdateProfileResponse>
{
    private readonly IAxonPrincipalWriteRepository _principalRepository;
    private readonly ILogger<UpdateProfileHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public UpdateProfileHandler(
        ICurrentUserService currentUserService,
        IAxonPrincipalWriteRepository principalRepository,
        TimeProvider timeProvider,
        ILogger<UpdateProfileHandler> logger)
        : base(currentUserService)
    {
        _principalRepository = principalRepository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public override async Task<Result<UpdateProfileResponse, Error>> Handle(
        UpdateProfileCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing UpdateProfile command for principal {PrincipalId}", command.AxonId);

        // Validate that at least one field is provided
        if (string.IsNullOrEmpty(command.PreferredLanguage) && string.IsNullOrEmpty(command.RiskTier))
        {
            return Result.Failure<UpdateProfileResponse, Error>(
                IdentityDomainErrors.Validation.AtLeastOneFieldRequired());
        }

        // Get the principal
        var principal = await _principalRepository.GetByIdAsync(command.AxonId, cancellationToken);
        if (principal is null)
        {
            return Result.Failure<UpdateProfileResponse, Error>(
                IdentityDomainErrors.Principal.NotFound());
        }

        // Track changes
        var languageChanged = false;
        var riskTierChanged = false;

        // Update preferred language if provided
        if (!string.IsNullOrEmpty(command.PreferredLanguage))
        {
            var languageResult = PreferredLanguage.Create(command.PreferredLanguage);
            if (languageResult.IsFailure)
            {
                return Result.Failure<UpdateProfileResponse, Error>(languageResult.Error);
            }

            // Check if language is different from current
            if (principal.Profile.PreferredLanguage != languageResult.Value)
            {
                var updateLanguageResult = principal.UpdatePreferredLanguage(languageResult.Value, _timeProvider);
                if (updateLanguageResult.IsFailure)
                {
                    return Result.Failure<UpdateProfileResponse, Error>(updateLanguageResult.Error);
                }
                languageChanged = true;
            }
        }

        // Update risk tier if provided
        if (!string.IsNullOrEmpty(command.RiskTier))
        {
            var riskTierResult = RiskTier.Create(command.RiskTier);
            if (riskTierResult.IsFailure)
            {
                return Result.Failure<UpdateProfileResponse, Error>(riskTierResult.Error);
            }

            // Check if risk tier is different from current
            if (principal.Profile.RiskTier != riskTierResult.Value)
            {
                // Enforce service principal constraints
                if (principal.IsService && riskTierResult.Value != RiskTier.Conservative)
                {
                    return Result.Failure<UpdateProfileResponse, Error>(
                        IdentityDomainErrors.Profile.ServicePrincipalRiskConstraint());
                }

                var updateRiskTierResult = principal.UpdateRiskTier(riskTierResult.Value, _timeProvider);
                if (updateRiskTierResult.IsFailure)
                {
                    return Result.Failure<UpdateProfileResponse, Error>(updateRiskTierResult.Error);
                }
                riskTierChanged = true;
            }
        }

        // Save changes if any were made
        if (languageChanged || riskTierChanged)
        {
            await _principalRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Determine status
        var status = (languageChanged || riskTierChanged) 
            ? UpdateProfileStatus.Updated 
            : UpdateProfileStatus.NoOp;

        return Result.Success<UpdateProfileResponse, Error>(new UpdateProfileResponse(
            AxonId: principal.Id,
            PreferredLanguage: principal.Profile.PreferredLanguage.Value,
            RiskTier: principal.Profile.RiskTier.Value,
            Changed: new ProfileChanges(languageChanged, riskTierChanged),
            Status: status
        ));
    }
}