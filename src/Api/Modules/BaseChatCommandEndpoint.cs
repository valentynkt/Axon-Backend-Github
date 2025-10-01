using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Endpoints.Base;
using CSharpFunctionalExtensions;

namespace Axon.Api.Modules;

/// <summary>
/// Base class for chat command endpoints that provides standardized configuration and command execution patterns
/// </summary>
public abstract class BaseChatCommandEndpoint<TRequest, TResponse, TCommand, TDomainResult>
    : BaseMappedEndpoint<TRequest, TResponse>
    where TRequest : notnull
    where TCommand : notnull
    where TDomainResult : notnull
{
    protected BaseChatCommandEndpoint(ILogger logger) : base(logger)
    {
    }

    public override void Configure()
    {
        Post(GetRoute());
        Policies("DynamicOrAxon");  // Accept both Dynamic and Axon tokens

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

        Tags("Chat");
    }

    protected override async Task<Result<TResponse, Error>> ExecuteAsync(
        TRequest request,
        CancellationToken ct)
    {
        return await MapExecuteMap<TCommand, TDomainResult>(
            request,
            ExecuteCommand,
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

    /// <summary>
    /// Override to implement the command execution logic
    /// </summary>
    protected abstract Task<Result<TDomainResult, Error>> ExecuteCommand(TCommand command, CancellationToken ct);
}