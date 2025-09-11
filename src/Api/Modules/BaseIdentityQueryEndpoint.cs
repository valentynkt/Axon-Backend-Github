using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Endpoints.Base;
using BuildingBlocks.Web.Extensions;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Api.Modules;

/// <summary>
/// Base class for identity query endpoints that provides mapping and standardized identity configuration with ETag support
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
            s.Responses[304] = "Not Modified - Content hasn't changed since last request (ETag match)";
            s.Responses[400] = "Invalid request parameters";
            s.Responses[401] = "User not authenticated";
            s.Responses[403] = "User does not have access to this resource";
            s.Responses[404] = "Resource not found";
            s.Responses[500] = "Internal server error";
        });

        Tags("Authentication");
    }

    public override async Task HandleAsync(TRequest req, CancellationToken ct)
    {
        LogRequestReceived();

        try
        {
            var result = await ExecuteAsync(req, ct);
            
            if (result.IsSuccess)
            {
                LogRequestCompleted();
                
                // Add ETag and Cache-Control headers for successful responses
                await HandleSuccessfulResponse(result.Value, ct);
            }
            else
            {
                // Check if this is a 304 Not Modified response
                if (result.Error.Metadata?.TryGetValue("IsNotModified", out var isNotModified) == true && 
                    isNotModified is true &&
                    result.Error.Metadata.TryGetValue("ETag", out var etagMetadata))
                {
                    var etag = etagMetadata?.ToString() ?? string.Empty;
                    await HandleNotModifiedResponse(etag, ct);
                }
                else
                {
                    Logger.LogWarning(
                        "Request failed with domain error: {ErrorCode} - {ErrorMessage} (traceId={TraceId})",
                        result.Error.Code,
                        result.Error.Message,
                        HttpContext.TraceIdentifier);
                        
                    await HttpContext.SendProblemDetailsAsync(result.Error, ct);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            LogRequestCancelled();
            throw;
        }
        catch (Exception ex)
        {
            LogRequestFailed(ex);
            await HttpContext.SendProblemDetailsAsync(
                Error.Internal(
                    $"An unexpected error occurred: {ex.Message}",
                    "UNEXPECTED_ERROR",
                    ex),
                ct);
        }
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
            
        // Extract ETag from domain result before mapping
        var etag = ExtractETagFromDomainResult(domainResult.Value);
        if (!string.IsNullOrEmpty(etag))
        {
            // Store ETag in HttpContext.Items for use in HandleSuccessfulResponse
            HttpContext.Items["ETag"] = etag;
        }
            
        return MapResponse<TDomainResult>(domainResult.Value);
    }

    /// <summary>
    /// Handles successful response with ETag and Cache-Control headers
    /// </summary>
    private Task HandleSuccessfulResponse(TResponse response, CancellationToken _)
    {
        // Get ETag from HttpContext.Items (set during ExecuteAsync)
        var etag = HttpContext.Items["ETag"]?.ToString();
        if (!string.IsNullOrEmpty(etag))
        {
            HttpContext.Response.Headers.ETag = $"\"{etag}\"";
        }

        // Add Cache-Control header for client guidance
        HttpContext.Response.Headers.CacheControl = "private, max-age=0, must-revalidate";
        
        Response = response;
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles 304 Not Modified response with appropriate headers
    /// </summary>
    private Task HandleNotModifiedResponse(string etag, CancellationToken _)
    {
        Logger.LogDebug("Returning 304 Not Modified for ETag: {ETag} (traceId={TraceId})", 
            etag, HttpContext.TraceIdentifier);

        HttpContext.Response.StatusCode = StatusCodes.Status304NotModified;
        HttpContext.Response.Headers.ETag = $"\"{etag}\"";
        HttpContext.Response.Headers.CacheControl = "private, max-age=0, must-revalidate";
        
        // 304 responses must not have a body - framework handles this automatically
        return Task.CompletedTask;
    }

    /// <summary>
    /// Override in derived classes to extract ETag from successful domain results
    /// </summary>
    protected virtual string? ExtractETagFromDomainResult(TDomainResult domainResult) => null;

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