using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Common.Commands;

/// <summary>
/// Base handler for idempotent Identity module commands.
/// Extends the base command handler to support idempotent operations.
/// </summary>
/// <typeparam name="TCommand">The command type that inherits from IdentityIdempotentCommand</typeparam>
/// <typeparam name="TResponse">The response type returned by the command</typeparam>
public abstract class BaseIdentityIdempotentCommandHandler<TCommand, TResponse> : BaseIdentityCommandHandler<TCommand, TResponse>
    where TCommand : IdentityIdempotentCommand<TResponse>
    where TResponse : notnull
{
    protected BaseIdentityIdempotentCommandHandler(ICurrentUserService currentUserService)
        : base(currentUserService)
    {
    }

    /// <summary>
    /// Handles the idempotent command execution. Must be implemented by derived classes.
    /// Idempotency is handled by pipeline behavior.
    /// </summary>
    /// <param name="request">The idempotent command request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the response or error</returns>
    public abstract override Task<Result<TResponse, Error>> Handle(TCommand request, CancellationToken cancellationToken);
}