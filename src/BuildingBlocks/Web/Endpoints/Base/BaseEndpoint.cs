using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Web.Endpoints.Base;

/// <summary>
/// Base class for all FastEndpoints providing common functionality.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public abstract class BaseEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
    where TRequest : notnull
{
    protected ILogger Logger { get; }

    protected BaseEndpoint(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Logs the incoming request with trace information.
    /// </summary>
    protected virtual void LogRequestReceived()
    {
        Logger.LogInformation(
            "Request received for {EndpointName} (traceId={TraceId})",
            GetType().Name,
            HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// Logs successful request completion.
    /// </summary>
    protected virtual void LogRequestCompleted()
    {
        Logger.LogInformation(
            "Request completed successfully for {EndpointName} (traceId={TraceId})",
            GetType().Name,
            HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// Logs request failure with error details.
    /// </summary>
    protected virtual void LogRequestFailed(Exception exception)
    {
        Logger.LogError(exception,
            "Request failed for {EndpointName} (traceId={TraceId})",
            GetType().Name,
            HttpContext.TraceIdentifier);
    }

    /// <summary>
    /// Logs cancellation by client.
    /// </summary>
    protected virtual void LogRequestCancelled()
    {
        Logger.LogWarning(
            "Request was cancelled by the client for {EndpointName} (traceId={TraceId})",
            GetType().Name,
            HttpContext.TraceIdentifier);
    }
}