// /Modules/Chat/Application/Common/Queries/BaseChatQueryHandler.cs
#nullable enable
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Application.Common.Queries;

/// <summary>
/// Base class for Chat query handlers providing common functionality.
/// Encapsulates authentication-related operations and user ID extraction.
/// Pipeline behaviors handle the actual authentication validation.
/// </summary>
public abstract class BaseChatQueryHandler
{
    private readonly ICurrentUserService _currentUserService;

    protected BaseChatQueryHandler(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Gets the authenticated user ID from the current user service.
    /// This method assumes authentication has been validated by AuthenticationBehavior.
    /// </summary>
    /// <returns>The authenticated user's ID</returns>
    protected UserId GetAuthenticatedUserId()
    {
        // AuthenticationBehavior ensures UserId is not null for authenticated requests
        var userIdString = _currentUserService.UserId!;
        return new UserId(Guid.Parse(userIdString));
    }
}