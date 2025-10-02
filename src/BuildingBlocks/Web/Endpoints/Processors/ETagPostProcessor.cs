using BuildingBlocks.Web.Contracts;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Web.Endpoints.Processors;

/// <summary>
/// Post-processor that adds ETag headers to successful responses.
/// 304 Not Modified responses are handled at the endpoint/service layer.
/// </summary>
public sealed class ETagPostProcessor : IGlobalPostProcessor
{
    public Task PostProcessAsync(IPostProcessorContext context, CancellationToken ct)
    {
        // Only add ETag headers if response implements IHaveETag and has an ETag value
        if (context.Response is IHaveETag etagEntity && !string.IsNullOrEmpty(etagEntity.ETag))
        {
            // Add ETag and Cache-Control headers for successful responses
            context.HttpContext.Response.Headers.ETag = $"\"{etagEntity.ETag}\"";
            context.HttpContext.Response.Headers.CacheControl = "private, max-age=0, must-revalidate";
        }

        return Task.CompletedTask;
    }
}
