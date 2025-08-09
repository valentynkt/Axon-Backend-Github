using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Abstractions.CQRS;

/// <summary>
/// Abstract base record for commands without return values.
/// Provides consistent implementation for command identification and timestamps.
/// Uses record type for value equality and immutability benefits.
/// Commands return Result pattern for consistent error handling.
/// </summary>
public abstract record CommandBase : RequestBase<Result<Unit>>, ICommand
{
}

/// <summary>
/// Abstract base record for commands that return a response wrapped in Result pattern.
/// Combines base command functionality with type-safe response handling and error management.
/// </summary>
/// <typeparam name="TResponse">The type of response this command produces</typeparam>
public abstract record CommandBase<TResponse> : RequestBase<Result<TResponse>>, ICommand<TResponse>
    where TResponse : notnull
{
}