using BuildingBlocks.Web.Contracts;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Web.Endpoints.Processors;

/// <summary>
/// Post-processor that handles ETag injection and 304 Not Modified responses
/// Works with responses implementing IHaveETag
/// </summary>
public sealed class ETagPostProcessor : IGlobalPostProcessor
{
    private readonly ILogger<ETagPostProcessor> _logger;

    public ETagPostProcessor(ILogger<ETagPostProcessor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PostProcessAsync(IPostProcessorContext context, CancellationToken ct)
    {
        // Only process if response implements IHaveETag
        if (context.Response is not IHaveETag etagEntity || string.IsNullOrEmpty(etagEntity.ETag))
            return;

        var serverETag = etagEntity.ETag;
        var clientETag = context.HttpContext.Items["ClientETag"]?.ToString();
        var httpContext = context.HttpContext;

        // Check if ETags match (client has current version)
        if (!string.IsNullOrEmpty(clientETag) && clientETag == serverETag)
        {
            _logger.LogDebug(
                "Returning 304 Not Modified for ETag: {ETag} (traceId={TraceId})",
                serverETag,
                httpContext.TraceIdentifier);

            // Client has current version - return 304
            httpContext.MarkResponseStart();
            httpContext.Response.StatusCode = StatusCodes.Status304NotModified;
            httpContext.Response.Headers.ETag = $"\"{serverETag}\"";
            httpContext.Response.Headers.CacheControl = "private, max-age=0, must-revalidate";
            httpContext.Response.ContentLength = 0;

            // Clear response body (don't send it)
            await httpContext.Response.WriteAsync("", ct);
        }
        else
        {
            // ETags don't match or no client ETag - send full response with new ETag
            httpContext.Response.Headers.ETag = $"\"{serverETag}\"";
            httpContext.Response.Headers.CacheControl = "private, max-age=0, must-revalidate";
        }
    }
}
