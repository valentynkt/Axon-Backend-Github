using BuildingBlocks.Web.Mappers;
using CSharpFunctionalExtensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
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
    protected IMapperFactory MapperFactory { get; }

    protected BaseCommandEndpoint(
        IMediator mediator,
        ILogger logger,
        IMapperFactory mapperFactory) 
        : base(logger)
    {
        Mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        MapperFactory = mapperFactory ?? throw new ArgumentNullException(nameof(mapperFactory));
    }

    protected override async Task<Result<TResponse, Error>> ExecuteAsync(TRequest request, CancellationToken cancellationToken)
    {
        // Get mappers from factory
        var requestMapper = MapperFactory.GetRequestMapper<TRequest, TCommand>();
        var responseMapper = MapperFactory.GetResponseMapper<TCommandResult, TResponse>();

        // Map HTTP request to domain command
        var commandResult = await requestMapper.MapAsync(request, cancellationToken);
        if (commandResult.IsFailure)
            return Result.Failure<TResponse, Error>(commandResult.Error);

        // Execute the command via MediatR
        var result = await Mediator.Send(commandResult.Value, cancellationToken);
        if (result is Result<TCommandResult, Error> typedResult)
        {
            if (typedResult.IsFailure)
                return Result.Failure<TResponse, Error>(typedResult.Error);

            // Map domain result to HTTP response
            var responseResult = await responseMapper.MapAsync(typedResult.Value, cancellationToken);
            return responseResult;
        }

        throw new InvalidOperationException($"Command {typeof(TCommand).Name} must return Result<{typeof(TCommandResult).Name}, Error>");
    }

    public override void Configure()
    {
        // Default configuration for command endpoints
        Post(GetRoute());
        
        // Common command configurations
        Options(x => x.WithTags(GetTags()));
        
        var summary = GetSummary();
        if (summary is not null)
            Summary(summary);
    }

    /// <summary>
    /// Override to specify the route for this command endpoint
    /// </summary>
    protected abstract string GetRoute();

    /// <summary>
    /// Override to specify tags for OpenAPI grouping
    /// </summary>
    protected virtual string[] GetTags() => [];

    /// <summary>
    /// Override to provide endpoint summary for OpenAPI
    /// </summary>
    protected virtual Action<EndpointSummary>? GetSummary() => null;
}