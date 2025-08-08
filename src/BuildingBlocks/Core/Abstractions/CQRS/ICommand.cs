using Unit = BuildingBlocks.Core.Functional.Unit;

namespace BuildingBlocks.Core.Abstractions.CQRS;

/// <summary>
/// Interface for commands that don't return a value.
/// Commands represent write operations that change system state.
/// Follows CQS principle by separating commands from queries.
/// Uses Result pattern for consistent error handling.
/// </summary>
public interface ICommand : ICommand<Unit>
{
}

/// <summary>
/// Interface for commands that return a response wrapped in Result pattern.
/// Enables scenarios where command execution results need to be communicated back.
/// All command results are wrapped in Result&lt;T&gt; for consistent error handling.
/// </summary>
/// <typeparam name="TResponse">The type of response this command produces</typeparam>
public interface ICommand<TResponse> : IAxonRequest<Result<TResponse>>, MediatR.IRequest<Result<TResponse>>
    where TResponse : notnull
{
}