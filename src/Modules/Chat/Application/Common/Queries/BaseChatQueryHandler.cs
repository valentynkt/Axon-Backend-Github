using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Chat.Application.Common.Queries;

/// <summary>
/// Base class for all Chat module query handlers.
/// Provides common functionality and implements IQueryHandler interface for type safety.
/// </summary>
/// <typeparam name="TQuery">The query type that inherits from ChatBaseQuery</typeparam>
/// <typeparam name="TResponse">The response type the query handler returns</typeparam>
public abstract class BaseChatQueryHandler<TQuery, TResponse> : IQueryHandler<TQuery, TResponse>
    where TQuery : ChatBaseQuery<TResponse>
    where TResponse : notnull
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

    /// <summary>
    /// Handles the query execution. Must be implemented by derived classes.
    /// </summary>
    /// <param name="request">The query request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the response or error</returns>
    public abstract Task<Result<TResponse, Error>> Handle(TQuery request, CancellationToken cancellationToken);
}