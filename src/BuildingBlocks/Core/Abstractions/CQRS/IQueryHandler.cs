using BuildingBlocks.Core.Functional.Results;
using MediatR;

namespace BuildingBlocks.Core.Abstractions.CQRS;

/// <summary>Query handler returning Result&lt;TResponse&gt;.</summary>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
    where TResponse : notnull
{
}