using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using MediatR;
using Unit = BuildingBlocks.Core.Functional.Unit;

namespace BuildingBlocks.Core.Abstractions.CQRS;

/// <summary>Command handler for commands returning Result&lt;Unit&gt;.</summary>
public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, Unit>
    where TCommand : ICommand<Unit>
{
}

/// <summary>Command handler for commands returning Result&lt;TResponse&gt;.</summary>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>
    where TResponse : notnull
{
}