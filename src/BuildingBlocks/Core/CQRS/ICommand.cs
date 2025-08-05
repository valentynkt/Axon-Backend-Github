using MediatR;

namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// Interface for commands that don't return a value.
/// Commands represent write operations that change system state.
/// Follows CQS principle by separating commands from queries.
/// </summary>
public interface ICommand : ICommand<Unit>
{
}

/// <summary>
/// Interface for commands that return a response.
/// Enables scenarios where command execution results need to be communicated back.
/// </summary>
/// <typeparam name="TResponse">The type of response this command produces</typeparam>
public interface ICommand<out TResponse> : IAxonRequest<TResponse>, MediatR.IRequest<TResponse>
    where TResponse : notnull
{
}