using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Extensions;
using BuildingBlocks.Web.ProblemDetails;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Web.Endpoints.Base;

/// <summary>
/// Base endpoint that integrates with the Result pattern for consistent error handling.
/// Request/response logging is handled by LoggingPreProcessor and LoggingPostProcessor.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public abstract class BaseResultEndpoint<TRequest, TResponse> : BaseEndpoint<TRequest, TResponse>
    where TRequest : notnull
{
    protected BaseResultEndpoint(ILogger logger) : base(logger)
    {
    }

    public override async Task HandleAsync(TRequest req, CancellationToken ct)
    {
        try
        {
            var result = await ExecuteAsync(req, ct);

            if (result.IsSuccess)
            {
                // Validate result value
                if (result.Value == null)
                {
                    Logger.LogError("CRITICAL: result.Value is NULL despite IsSuccess=true. Type: {Type}",
                        typeof(TResponse).Name);
                    await SendProblemDetailsAsync(
                        Error.Internal("Response is null", "NULL_RESPONSE"), ct);
                    return;
                }

                // Set Response property - FastEndpoints will handle serialization
                // NOTE: Post-processors run AFTER this, so they can intercept and modify the response
                // (e.g., ETagPostProcessor can return 304 and clear the body)
                Response = result.Value;
            }
            else
            {
                // Check if this is a 304 Not Modified response (from ETag matching in service layer)
                if (result.Error.Metadata?.TryGetValue("IsNotModified", out var isNotModified) == true &&
                    isNotModified is true &&
                    result.Error.Metadata.TryGetValue("ETag", out var etagMetadata))
                {
                    var etag = etagMetadata?.ToString() ?? string.Empty;
                    await HandleNotModifiedResponseAsync(etag, ct);
                }
                else
                {
                    // Log domain error before sending ProblemDetails
                    Logger.LogWarning(
                        "Request failed with domain error: {ErrorCode} - {ErrorMessage}",
                        result.Error.Code,
                        result.Error.Message);

                    await SendProblemDetailsAsync(result.Error, ct);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Re-throw cancellation - LoggingPostProcessor handles logging
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error in endpoint execution");
            await SendProblemDetailsAsync(
                Error.Internal(
                    $"An unexpected error occurred: {ex.Message}",
                    "UNEXPECTED_ERROR",
                    ex),
                ct);
        }
    }

    /// <summary>
    /// Sends a problem details response using proper FastEndpoints methods.
    /// Marks response as started to prevent auto-204 NoContent.
    /// </summary>
    private async Task SendProblemDetailsAsync(Error error, CancellationToken ct)
    {
        var problemDetails = error.ToProblemDetails(
            instance: HttpContext.Request.Path,
            traceId: HttpContext.TraceIdentifier);

        // Mark response as started before writing to prevent FastEndpoints auto-response
        HttpContext.MarkResponseStart();

        HttpContext.Response.StatusCode = problemDetails.Status ?? 500;
        HttpContext.Response.ContentType = "application/problem+json";

        await HttpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken: ct);
    }

    /// <summary>
    /// Handles 304 Not Modified responses for ETag-based caching.
    /// </summary>
    private Task HandleNotModifiedResponseAsync(string etag, CancellationToken _)
    {
        Logger.LogDebug("Returning 304 Not Modified for ETag: {ETag} (traceId={TraceId})",
            etag, HttpContext.TraceIdentifier);

        // Set headers
        HttpContext.Response.Headers.ETag = $"\"{etag}\"";
        HttpContext.Response.Headers.CacheControl = "private, max-age=0, must-revalidate";
        HttpContext.Response.ContentLength = 0;

        // Set status code
        HttpContext.Response.StatusCode = StatusCodes.Status304NotModified;

        // CRITICAL: Mark response as started to prevent FastEndpoints from auto-serializing
        // Since response is started, FastEndpoints won't serialize Response property
        HttpContext.MarkResponseStart();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the business logic and returns a Result.
    /// </summary>
    /// <param name="request">The incoming request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A Result containing the response or an error</returns>
    protected abstract new Task<Result<TResponse, Error>> ExecuteAsync(TRequest request, CancellationToken cancellationToken);
}