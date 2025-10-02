using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Web.Endpoints.Base;

/// <summary>
/// Base class for all FastEndpoints providing common logging infrastructure.
/// Request/response logging is handled by LoggingPreProcessor and LoggingPostProcessor.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public abstract class BaseEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Logger instance for endpoint-specific logging.
    /// General request/response logging is handled by global processors.
    /// </summary>
    protected new ILogger Logger { get; }

    protected BaseEndpoint(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}