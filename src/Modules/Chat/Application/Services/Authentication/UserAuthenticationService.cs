using System;
using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.Contracts.Authentication;
using Axon.Modules.Chat.Domain.Services;
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

    public UserAuthenticationService(ICurrentUserService currentUser)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public Result<UserId, Error> GetAuthenticatedUserId()
    {
        return UserAuthenticationDomainService.ValidateAndCreateUserId(
            _currentUser.IsAuthenticated, 
            _currentUser.UserId);
    }
}