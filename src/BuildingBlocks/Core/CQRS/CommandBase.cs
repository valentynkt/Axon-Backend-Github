using MediatR;
using MassTransit;

namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// Abstract base record for commands without return values.
/// Provides consistent implementation for command identification and timestamps.
/// Uses record type for value equality and immutability benefits.
/// </summary>
public abstract record CommandBase : RequestBase<Unit>, ICommand
{
}

/// <summary>
/// Abstract base record for commands that return a response.
/// Combines base command functionality with type-safe response handling.
/// </summary>
/// <typeparam name="TResponse">The type of response this command produces</typeparam>
public abstract record CommandBase<TResponse> : RequestBase<TResponse>, ICommand<TResponse>
    where TResponse : notnull
{
}