using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Web.Extensions;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Web.Builders;

/// <summary>
/// Default implementation of endpoint response builder.
/// </summary>
public sealed class EndpointResponseBuilder : IEndpointResponseBuilder
{
    public async Task SendSuccessAsync<T>(HttpContext context, T payload, int statusCode = 200, CancellationToken cancellationToken = default)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(payload, cancellationToken);
    }

    public async Task SendCreatedAsync<T>(HttpContext context, T payload, string location, CancellationToken cancellationToken = default)
    {
        context.Response.StatusCode = StatusCodes.Status201Created;
        context.Response.Headers.Location = location;
        await context.Response.WriteAsJsonAsync(payload, cancellationToken);
    }

    public Task SendNoContentAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        context.Response.StatusCode = StatusCodes.Status204NoContent;
        return Task.CompletedTask;
    }

    public async Task SendErrorAsync(HttpContext context, BuildingBlocks.Core.Diagnostics.Errors.Error error, CancellationToken cancellationToken = default)
    {
        await context.SendProblemDetailsAsync(error, cancellationToken);
    }

    public async Task HandleResultAsync<T>(HttpContext context, Result<T> result, int successStatusCode = 200, CancellationToken cancellationToken = default)
    {
        if (result.IsSuccess)
        {
            await SendSuccessAsync(context, result.Value, successStatusCode, cancellationToken);
        }
        else
        {
            await SendErrorAsync(context, result.Error, cancellationToken);
        }
    }

    public async Task HandleResultAsync(HttpContext context, Result result, int successStatusCode = 200, CancellationToken cancellationToken = default)
    {
        if (result.IsSuccess)
        {
            context.Response.StatusCode = successStatusCode;
        }
        else
        {
            await SendErrorAsync(context, result.Error, cancellationToken);
        }
    }
}