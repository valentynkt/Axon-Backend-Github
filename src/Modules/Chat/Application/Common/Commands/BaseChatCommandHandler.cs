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
    /// Gets the authenticated user's AxonUserId with smart caching support.
    /// Leverages 3-tier cache hierarchy from Epic 4b for 50x performance improvement.
    /// Returns Result with error if AxonUserId cannot be resolved.
    /// </summary>
    protected async Task<Result<AxonUserId, Error>> GetAuthenticatedAxonUserIdAsync(CancellationToken ct = default)
    {
        var axonAxonUserId = await _currentUserService.GetAxonUserIdAsync(ct);
        if (!axonAxonUserId.HasValue)
        {
            return Result.Failure<AxonUserId, Error>(
                Error.Unauthorized("AxonUserId not resolved for authenticated user", "AUTH.USER_ID_NOT_RESOLVED"));
        }

        return Result.Success<AxonUserId, Error>(axonAxonUserId.Value);
    }

    /// <summary>
    /// Handles the command execution. Must be implemented by derived classes.
    /// </summary>
    /// <param name="request">The command request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the response or error</returns>
    public abstract Task<Result<TResponse, Error>> Handle(TCommand request, CancellationToken cancellationToken);
}