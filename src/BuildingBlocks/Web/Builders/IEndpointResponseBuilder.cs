using BuildingBlocks.Core.Functional.Results;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Web.Builders;

/// <summary>
/// Builds consistent HTTP responses for endpoints.
/// </summary>
public interface IEndpointResponseBuilder
{
    /// <summary>
    /// Sends a successful response with the specified payload.
    /// </summary>
    Task SendSuccessAsync<T>(HttpContext context, T payload, int statusCode = 200, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a created response (201) with location header.
    /// </summary>
    Task SendCreatedAsync<T>(HttpContext context, T payload, string location, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a no content response (204).
    /// </summary>
    Task SendNoContentAsync(HttpContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an error response using Problem Details format.
    /// </summary>
    Task SendErrorAsync(HttpContext context, BuildingBlocks.Core.Diagnostics.Errors.Error error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles a Result automatically, sending success or error response.
    /// </summary>
    Task HandleResultAsync<T>(HttpContext context, Result<T> result, int successStatusCode = 200, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles a Result automatically, sending success status or error response.
    /// </summary>
    Task HandleResultAsync(HttpContext context, Result result, int successStatusCode = 200, CancellationToken cancellationToken = default);
}