// /BuildingBlocks/Core/Abstractions/CQRS/IQuery.cs
#nullable enable
using CSharpFunctionalExtensions;
using BuildingBlocks.Core.Diagnostics.Errors;
using MediatR;

namespace BuildingBlocks.Core.Abstractions.CQRS;

/// <summary>
/// Query that reads state and returns a value; unified on Result&lt;TResponse, Error&gt;.
/// </summary>
public interface IQuery<TResponse> : IAxonRequest, IRequest<Result<TResponse, Error>>
    where TResponse : notnull { }

/// <summary>
/// Handler for processing queries that return Results with error handling.
/// </summary>
/// <typeparam name="TQuery">The query type to handle</typeparam>
/// <typeparam name="TResponse">The response type to return</typeparam>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse, Error>>
    where TQuery : IQuery<TResponse>
    where TResponse : notnull
{
}