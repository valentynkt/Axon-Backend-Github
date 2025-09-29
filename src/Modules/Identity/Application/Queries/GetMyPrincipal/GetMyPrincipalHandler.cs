using Axon.Modules.Identity.Application.Common.Queries;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.DTOs.Responses;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Queries.GetMyPrincipal;

/// <summary>
/// Handler for GetMyPrincipalQuery that delegates to UserProfileService
/// following Single Responsibility Principle. This handler is now a thin
/// orchestration layer focused only on MediatR command/query handling.
/// </summary>
public sealed class GetMyPrincipalHandler : BaseIdentityQueryHandler<GetMyPrincipalQuery, CurrentUserResult>
{
    private readonly IUserProfileService _userProfileService;
    private readonly ILogger<GetMyPrincipalHandler> _logger;

    public GetMyPrincipalHandler(
        ICurrentUserService currentUserService,
        IUserProfileService userProfileService,
        ILogger<GetMyPrincipalHandler> logger) : base(currentUserService)
    {
        _userProfileService = userProfileService ?? throw new ArgumentNullException(nameof(userProfileService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<Result<CurrentUserResult, Error>> Handle(
        GetMyPrincipalQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing GetMyPrincipal query for principal {PrincipalId}", query.PrincipalId.Value);

        // Delegate to UserProfileService - this handler is now just a thin orchestration layer
        var result = await _userProfileService.GetCurrentUserProfileAsync(
            query.PrincipalId,
            query.IfNoneMatch,
            cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogDebug("Successfully retrieved user profile for principal {PrincipalId}", query.PrincipalId.Value);
        }
        else
        {
            _logger.LogDebug("Failed to retrieve user profile for principal {PrincipalId}: {Error}",
                query.PrincipalId.Value, result.Error);
        }

        return result;
    }

}