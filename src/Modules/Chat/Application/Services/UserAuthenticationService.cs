using System;
using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Abstractions.Telemetry;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Chat.Application.Services;

/// <summary>
/// Service for authenticating and validating the current user in chat operations.
/// </summary>
public sealed class UserAuthenticationService : IUserAuthenticationService
{
    private readonly ICurrentUserService _currentUser;
    private readonly IAppTelemetry? _telemetry;

    public UserAuthenticationService(
        ICurrentUserService currentUser,
        IAppTelemetry? telemetry = null)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _telemetry = telemetry;
    }

    public Result<UserId, Error> GetAuthenticatedUserId()
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            _telemetry?.TrackValidationFailure("UserAuthentication", "CHAT.AUTH.UNAUTHENTICATED");
            return Result.Failure<UserId, Error>(
                Error.Unauthorized("User must be authenticated to perform chat operations.", "CHAT.AUTH.UNAUTHENTICATED"));
        }

        if (!Guid.TryParse(_currentUser.UserId, out var ownerGuid))
        {
            _telemetry?.TrackValidationFailure("UserAuthentication", "CHAT.AUTH.INVALID_USERID");
            return Result.Failure<UserId, Error>(
                Error.Validation("Invalid current user id.", "CHAT.AUTH.INVALID_USERID"));
        }

        return Result.Success<UserId, Error>(new UserId(ownerGuid));
    }
}