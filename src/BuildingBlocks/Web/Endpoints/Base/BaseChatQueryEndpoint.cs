using MediatR;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Contracts;
using BuildingBlocks.Web.Endpoints.Base;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Web.Endpoints.Base;

/// <summary>
/// Base class for chat query endpoints that provides pagination, mapping, and standardized chat configuration
/// </summary>
public abstract class BaseChatQueryEndpoint<TRequest, TResponse, TQuery, TDomainResult> 
    : BaseMappedEndpoint<TRequest, TResponse>
    where TRequest : BasePagedRequest
    where TQuery : IRequest<Result<TDomainResult, Error>>
    where TDomainResult : notnull
{
    private readonly IMediator _mediator;

    protected BaseChatQueryEndpoint(IMediator mediator, ILogger logger) : base(logger)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get(GetRoute());
        AllowAnonymous();

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

        Tags("Chat");
    }

    protected override async Task<Result<TResponse, Error>> ExecuteAsync(
        TRequest request,
        CancellationToken ct)
    {
        return await MapExecuteMap<TQuery, TDomainResult>(
            request,
            (query, cancellationToken) => _mediator.Send(query, cancellationToken),
            ct);
    }

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