using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Abstractions.CQRS;

/// <summary>Commands that do not return a value. Result&lt;Unit&gt; ensures consistent errors.</summary>
public interface ICommand : ICommand<Unit>
{
}

/// <summary>Commands that return a response wrapped in Result&lt;TResponse&gt;.</summary>
public interface ICommand<TResponse> : IAxonRequest<Result<TResponse>>, MediatR.IRequest<Result<TResponse>>
    where TResponse : notnull
{
}