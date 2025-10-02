using FastEndpoints;

namespace BuildingBlocks.Web.Endpoints.Processors;

/// <summary>
/// Pre-processor that extracts the If-None-Match header for ETag caching support
/// Stores the client's ETag in HttpContext.Items for later comparison
/// </summary>
public sealed class ETagPreProcessor : IGlobalPreProcessor
{
    public Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
    {
        var httpContext = context.HttpContext;

        // Extract client's ETag from If-None-Match header
        if (httpContext.Request.Headers.TryGetValue("If-None-Match", out var clientETag))
        {
            // Store for comparison in post-processor
            // Remove quotes if present: "abc123" -> abc123
            var etagValue = clientETag.ToString().Trim('"');
            httpContext.Items["ClientETag"] = etagValue;
        }

        return Task.CompletedTask;
    }
}
