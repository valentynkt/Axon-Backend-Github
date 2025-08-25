using CSharpFunctionalExtensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Web.Endpoints.Base;

/// <summary>
/// Base endpoint for query operations (GET requests)
/// </summary>
/// <typeparam name="TRequest">Request DTO type</typeparam>
/// <typeparam name="TResponse">Response DTO type</typeparam>
public abstract class BaseQueryEndpoint<TRequest, TResponse> : BaseResultEndpoint<TRequest, TResponse>
    where TRequest : notnull
{
    protected BaseQueryEndpoint(ILogger logger) : base(logger)
    {
    }

    /// <summary>
    /// Override this method to implement the query logic
    /// </summary>
    protected abstract Task<Result<TResponse, Error>> HandleQueryAsync(TRequest request, CancellationToken ct);

    protected override async Task<Result<TResponse, Error>> ExecuteAsync(TRequest request, CancellationToken cancellationToken)
    {
        return await HandleQueryAsync(request, cancellationToken);
    }

    public override void Configure()
    {
        // Default configuration for query endpoints
        Get(GetRoute());
        
        // Common query configurations
        Options(x => x.WithTags(GetTags()));
        
        var summary = GetSummary();
        if (summary is not null)
            Summary(summary);
            
        // Allow anonymous access as per architectural decision in Program.cs
        // All endpoints use AllowAnonymous() with DefaultCurrentUserService providing system user
        AllowAnonymous();
    }

    /// <summary>
    /// Override to specify the route for this query endpoint
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