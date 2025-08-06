using BuildingBlocks.Core.Results;
using MediatR;

namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// Interface for command handlers that don't return a value.
/// All command handlers return Result pattern for consistent error handling.
/// </summary>
/// <typeparam name="TCommand">The type of command to handle</typeparam>
public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, Unit>
    where TCommand : ICommand<Unit>
{
}

/// <summary>
/// Interface for command handlers that return a response wrapped in Result pattern.
/// Provides consistent error handling across all command operations.
/// </summary>
/// <typeparam name="TCommand">The type of command to handle</typeparam>
/// <typeparam name="TResponse">The type of response the command produces</typeparam>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>
    where TResponse : notnull
{
}