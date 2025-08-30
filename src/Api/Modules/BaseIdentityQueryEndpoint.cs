using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Endpoints.Base;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Api.Modules;

/// <summary>
/// Base class for identity query endpoints that provides mapping and standardized identity configuration
/// </summary>
public abstract class BaseIdentityQueryEndpoint<TRequest, TResponse, TQuery, TDomainResult> 
    : BaseMappedEndpoint<TRequest, TResponse>
    where TRequest : notnull
    where TQuery : IRequest<Result<TDomainResult, Error>>
    where TDomainResult : notnull
{
    private readonly IMediator _mediator;

    protected BaseIdentityQueryEndpoint(IMediator mediator, ILogger logger) : base(logger)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get(GetRoute());
        AllowAnonymous(); // TODO S2: Replace with RequireAuthorization() for Bearer token auth

        Summary(s =>
        {
            s.Summary = GetSummary();
            s.Description = GetDescription();
            s.Responses[200] = GetSuccessResponse();
            s.Responses[400] = "Invalid request parameters";
            s.Responses[401] = "User not authenticated";
            s.Responses[403] = "User does not have access to this resource";
            s.Responses[404] = "Resource not found";
            s.Responses[500] = "Internal server error";
        });

        Tags("Authentication");
    }

    protected override async Task<Result<TResponse, Error>> ExecuteAsync(
        TRequest request,
        CancellationToken ct)
    {
        var queryResult = await ExecuteQuery(request, ct);
        if (queryResult.IsFailure)
            return Result.Failure<TResponse, Error>(queryResult.Error);
            
        var domainResult = await _mediator.Send(queryResult.Value, ct);
        if (domainResult.IsFailure)
            return Result.Failure<TResponse, Error>(domainResult.Error);
            
        return MapResponse<TDomainResult>(domainResult.Value);
    }

    /// <summary>
    /// Override to extract and validate query parameters (e.g., JWT from headers)
    /// </summary>
    protected abstract Task<Result<TQuery, Error>> ExecuteQuery(TRequest request, CancellationToken ct);

    /// <summary>
    /// Override to specify the route for this endpoint
    /// </summary>
    protected abstract string GetRoute();

    /// <summary>
    /// Override to provide the summary for OpenAPI documentation
    /// </summary>
    protected abstract string GetSummary();

    /// <summary>
    /// Override to provide the description for OpenAPI documentation
    /// </summary>
    protected abstract string GetDescription();

    /// <summary>
    /// Override to provide the success response description
    /// </summary>
    protected abstract string GetSuccessResponse();
}