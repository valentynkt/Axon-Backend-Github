// /BuildingBlocks/Core/Abstractions/CQRS/ICommand.cs
#nullable enable
using CSharpFunctionalExtensions;
using BuildingBlocks.Core.Diagnostics.Errors;
using MediatR;

namespace BuildingBlocks.Core.Abstractions.CQRS;

/// <summary>
/// Command that mutates state; unified on Result&lt;Unit, Error&gt;.
/// </summary>
public interface ICommand : IAxonRequest, IRequest<Result<Unit, Error>> { }

/// <summary>
/// Command returning a value; unified on Result&lt;TResponse, Error&gt;.
/// </summary>
public interface ICommand<TResponse> : IAxonRequest, IRequest<Result<TResponse, Error>>
    where TResponse : notnull { }