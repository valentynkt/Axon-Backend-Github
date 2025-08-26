using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Chat.Application.Common.Commands;

/// <summary>
/// Base handler for Chat module commands that provides common functionality.
/// Handles authentication and provides helper methods for command execution.
/// </summary>
/// <typeparam name="TCommand">The command type that inherits from ChatBaseCommand</typeparam>
/// <typeparam name="TResponse">The response type returned by the command</typeparam>
public abstract class BaseChatCommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse, Error>>
    where TCommand : ChatBaseCommand<TResponse>
    where TResponse : notnull
{
    private readonly ICurrentUserService _currentUserService;

    protected BaseChatCommandHandler(ICurrentUserService currentUserService)
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
    /// Handles the command execution. Must be implemented by derived classes.
    /// </summary>
    /// <param name="request">The command request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the response or error</returns>
    public abstract Task<Result<TResponse, Error>> Handle(TCommand request, CancellationToken cancellationToken);
}