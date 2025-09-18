using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Identity.Application.Common.Commands;

/// <summary>
/// Base handler for Identity module commands that provides common functionality.
/// Handles authentication and provides helper methods for command execution.
/// </summary>
/// <typeparam name="TCommand">The command type that inherits from IdentityBaseCommand</typeparam>
/// <typeparam name="TResponse">The response type returned by the command</typeparam>
public abstract class BaseIdentityCommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse, Error>>
    where TCommand : IdentityBaseCommand<TResponse>
    where TResponse : notnull
{
    private readonly ICurrentUserService _currentUserService;

    protected BaseIdentityCommandHandler(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Gets the authenticated user ID from the current user service.
    /// This method assumes authentication has been validated by AuthenticationBehavior.
    /// </summary>
    /// <returns>The authenticated user's AxonUserId</returns>
    protected AxonUserId GetAuthenticatedAxonUserId()
    {
        // AuthenticationBehavior ensures UserId is not null for authenticated requests
        var userIdString = _currentUserService.UserId!;
        return new AxonUserId(Guid.Parse(userIdString));
    }

    /// <summary>
    /// Handles the command execution. Must be implemented by derived classes.
    /// </summary>
    /// <param name="request">The command request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the response or error</returns>
    public abstract Task<Result<TResponse, Error>> Handle(TCommand request, CancellationToken cancellationToken);
}