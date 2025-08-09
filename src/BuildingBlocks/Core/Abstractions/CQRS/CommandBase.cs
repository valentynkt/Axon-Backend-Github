using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;

namespace BuildingBlocks.Core.Abstractions.CQRS;

/// <summary>
/// Base record for commands without return values. Immutable and layer-agnostic.
/// </summary>
public abstract record CommandBase : RequestBase<Result<Unit>>, ICommand
{
}

/// <summary>
/// Base record for commands with a response wrapped in Result&lt;TResponse&gt;.
/// </summary>
public abstract record CommandBase<TResponse> : RequestBase<Result<TResponse>>, ICommand<TResponse>
    where TResponse : notnull
{
}