using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Endpoints.Base;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Api.Modules;

/// <summary>
/// Base class for identity command endpoints that provides mapping and standardized identity configuration
/// </summary>
public abstract class BaseIdentityCommandEndpoint<TRequest, TResponse, TCommand, TDomainResult> 
    : BaseMappedEndpoint<TRequest, TResponse>
    where TRequest : notnull
    where TCommand : IRequest<Result<TDomainResult, Error>>
    where TDomainResult : notnull
{
    private readonly IMediator _mediator;

    protected BaseIdentityCommandEndpoint(IMediator mediator, ILogger logger) : base(logger)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post(GetRoute());
        AllowAnonymous(); // TODO S2: Replace with RequireAuthorization() for Bearer token auth

        Summary(s =>
        {
            s.Summary = GetSummary();
            s.Description = GetDescription();
            s.Responses[200] = GetSuccessResponse();
            s.Responses[400] = "Invalid request parameters";
            s.Responses[401] = "User not authenticated";
            s.Responses[403] = "User does not have access to this resource";
            s.Responses[422] = "Business rule violation";
            s.Responses[500] = "Internal server error";
        });

        Tags("Authentication");
    }

    protected override async Task<Result<TResponse, Error>> ExecuteAsync(
        TRequest request,
        CancellationToken ct)
    {
        var commandResult = await ExecuteCommand(request, ct);
        if (commandResult.IsFailure)
            return Result.Failure<TResponse, Error>(commandResult.Error);
            
        var domainResult = await _mediator.Send(commandResult.Value, ct);
        if (domainResult.IsFailure)
            return Result.Failure<TResponse, Error>(domainResult.Error);
            
        return MapDomainToResponse(domainResult.Value);
    }
    
    /// <summary>
    /// Maps domain result to response. Override this if custom mapping is needed.
    /// </summary>
    protected virtual Result<TResponse, Error> MapDomainToResponse(TDomainResult domainResult)
    {
        return MapResponse<TDomainResult>(domainResult);
    }

    /// <summary>
    /// Override to extract and validate command parameters
    /// </summary>
    protected abstract Task<Result<TCommand, Error>> ExecuteCommand(TRequest request, CancellationToken ct);

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