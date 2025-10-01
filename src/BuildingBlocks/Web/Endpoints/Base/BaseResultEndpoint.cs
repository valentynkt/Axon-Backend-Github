using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Extensions;
using BuildingBlocks.Web.ProblemDetails;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Web.Endpoints.Base;

/// <summary>
/// Base endpoint that integrates with the Result pattern for consistent error handling.
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
        LogRequestReceived();

        try
        {
            var result = await ExecuteAsync(req, ct);

            if (result.IsSuccess)
            {
                LogRequestCompleted();

                // Debug: Check if result.Value is null
                if (result.Value == null)
                {
                    Logger.LogError("CRITICAL: result.Value is NULL despite IsSuccess=true. Type: {Type}",
                        typeof(TResponse).Name);
                    await SendProblemDetailsAsync(
                        Error.Internal("Response is null", "NULL_RESPONSE"), ct);
                    return;
                }

                Logger.LogInformation("Setting Response to value of type {Type}", result.Value.GetType().Name);
                Response = result.Value;
                Logger.LogInformation("Response has been set. Response is null: {IsNull}", Response == null);
            }
            else
            {
                Logger.LogWarning(
                    "Request failed with domain error: {ErrorCode} - {ErrorMessage} (traceId={TraceId})",
                    result.Error.Code,
                    result.Error.Message,
                    HttpContext.TraceIdentifier);

                await SendProblemDetailsAsync(result.Error, ct);
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
            await SendProblemDetailsAsync(
                Error.Internal(
                    $"An unexpected error occurred: {ex.Message}",
                    "UNEXPECTED_ERROR",
                    ex),
                ct);
        }
    }

    /// <summary>
    /// Sends a problem details response and marks response as handled to prevent auto-204.
    /// CRITICAL: Must set Response property to prevent FastEndpoints from sending 204 NoContent.
    /// </summary>
    private async Task SendProblemDetailsAsync(Error error, CancellationToken ct)
    {
        var problemDetails = error.ToProblemDetails(
            instance: HttpContext.Request.Path,
            traceId: HttpContext.TraceIdentifier);

        HttpContext.Response.StatusCode = problemDetails.Status ?? 500;
        HttpContext.Response.ContentType = "application/problem+json";

        // CRITICAL: Set Response to the problemDetails object to prevent FastEndpoints from
        // seeing null Response and sending 204 NoContent after HandleAsync completes
        Response = (TResponse)(object)problemDetails;

        await HttpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken: ct);
    }

    /// <summary>
    /// Executes the business logic and returns a Result.
    /// </summary>
    /// <param name="request">The incoming request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A Result containing the response or an error</returns>
    protected abstract new Task<Result<TResponse, Error>> ExecuteAsync(TRequest request, CancellationToken cancellationToken);
}