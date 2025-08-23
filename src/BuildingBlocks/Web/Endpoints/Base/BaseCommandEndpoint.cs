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

    protected BaseCommandEndpoint(
        IMediator mediator,
        ILogger logger) 
        : base(logger)
    {
        Mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    protected override async Task<Result<TResponse, Error>> ExecuteAsync(TRequest request, CancellationToken cancellationToken)
    {
        // Note: This base class is provided for future use.
        // When using it, override this method to:
        // 1. Map request to command using Mapster: var command = request.Adapt<TCommand>();
        // 2. Execute command via MediatR
        // 3. Map result to response using Mapster
        
        throw new NotImplementedException(
            "BaseCommandEndpoint.ExecuteAsync must be overridden. " +
            "Use Mapster's .Adapt<T>() for mapping between request/command and result/response.");
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