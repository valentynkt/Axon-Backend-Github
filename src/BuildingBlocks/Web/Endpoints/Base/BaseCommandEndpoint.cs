using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Mappers;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Web.Endpoints.Base;

/// <summary>
/// Base endpoint for CQRS command handling with automatic mapping.
/// </summary>
/// <typeparam name="TRequest">The HTTP request type</typeparam>
/// <typeparam name="TResponse">The HTTP response type</typeparam>
/// <typeparam name="TCommand">The CQRS command type</typeparam>
/// <typeparam name="TCommandResult">The command result type</typeparam>
public abstract class BaseCommandEndpoint<TRequest, TResponse, TCommand, TCommandResult> 
    : BaseResultEndpoint<TRequest, TResponse>
    where TRequest : notnull
    where TCommand : notnull
{
    protected IMediator Mediator { get; }
    protected IRequestMapper<TRequest, TCommand> RequestMapper { get; }
    protected IResponseMapper<TCommandResult, TResponse> ResponseMapper { get; }

    protected BaseCommandEndpoint(
        ILogger logger,
        IMediator mediator,
        IRequestMapper<TRequest, TCommand> requestMapper,
        IResponseMapper<TCommandResult, TResponse> responseMapper) 
        : base(logger)
    {
        Mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        RequestMapper = requestMapper ?? throw new ArgumentNullException(nameof(requestMapper));
        ResponseMapper = responseMapper ?? throw new ArgumentNullException(nameof(responseMapper));
    }

    protected override async Task<Result<TResponse, Error>> ExecuteAsync(TRequest request, CancellationToken cancellationToken)
    {
        // Map HTTP request to domain command
        var commandResult = await RequestMapper.MapAsync(request, cancellationToken);
        if (commandResult.IsFailure)
            return Result.Failure<TResponse, Error>(commandResult.Error);

        // Execute the command via MediatR
        var result = await Mediator.Send(commandResult.Value, cancellationToken);
        if (result is Result<TCommandResult, Error> typedResult)
        {
            if (typedResult.IsFailure)
                return Result.Failure<TResponse, Error>(typedResult.Error);

            // Map domain result to HTTP response
            var responseResult = await ResponseMapper.MapAsync(typedResult.Value, cancellationToken);
            return responseResult;
        }

        throw new InvalidOperationException($"Command {typeof(TCommand).Name} must return Result<{typeof(TCommandResult).Name}, Error>");
    }
}