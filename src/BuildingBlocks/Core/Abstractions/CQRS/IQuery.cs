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