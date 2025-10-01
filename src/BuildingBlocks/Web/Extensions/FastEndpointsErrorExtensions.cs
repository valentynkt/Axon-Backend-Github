using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.ProblemDetails;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Web.Extensions;

/// <summary>
/// Extensions for integrating Error system with FastEndpoints.
/// </summary>
public static class FastEndpointsErrorExtensions
{
    /// <summary>
    /// Sends a Problem Details response in FastEndpoints.
    /// </summary>
    /// <param name="httpContext">The HTTP context</param>
    /// <param name="error">The error to convert to Problem Details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task SendProblemDetailsAsync(
        this HttpContext httpContext,
        Error error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var problemDetails = error.ToProblemDetails(
            instance: httpContext.Request.Path,
            traceId: httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = problemDetails.Status ?? 500;
        httpContext.Response.ContentType = "application/problem+json";

        // CRITICAL: Check if response has already started to avoid writing multiple times
        if (!httpContext.Response.HasStarted)
        {
            await httpContext.Response.WriteAsJsonAsync(
                problemDetails,
                cancellationToken: cancellationToken);
        }
    }
    
    /// <summary>
    /// Sends a Validation Problem Details response in FastEndpoints.
    /// </summary>
    /// <param name="httpContext">The HTTP context</param>
    /// <param name="errors">The validation errors</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task SendValidationProblemDetailsAsync(
        this HttpContext httpContext,
        IEnumerable<Error> errors,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var validationDetails = errors.ToValidationProblemDetails(
            instance: httpContext.Request.Path,
            traceId: httpContext.TraceIdentifier);
            
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        httpContext.Response.ContentType = "application/problem+json";
        
        await httpContext.Response.WriteAsJsonAsync(
            validationDetails,
            cancellationToken: cancellationToken);
    }
    
    /// <summary>
    /// Handles a Result{T} in FastEndpoints, sending success or Problem Details response.
    /// </summary>
    /// <typeparam name="T">The success value type</typeparam>
    /// <param name="httpContext">The HTTP context</param>
    /// <param name="result">The result to handle</param>
    /// <param name="successStatusCode">Status code for success responses</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task HandleResultAsync<T>(
        this HttpContext httpContext,
        Result<T, Error> result,
        int successStatusCode = StatusCodes.Status200OK,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (result.IsSuccess)
        {
            httpContext.Response.StatusCode = successStatusCode;
            await httpContext.Response.WriteAsJsonAsync(result.Value, cancellationToken);
        }
        else
        {
            await httpContext.SendProblemDetailsAsync(result.Error, cancellationToken);
        }
    }
    
    /// <summary>
    /// Handles a Result in FastEndpoints, sending success status or Problem Details response.
    /// </summary>
    /// <param name="httpContext">The HTTP context</param>
    /// <param name="result">The result to handle</param>
    /// <param name="successStatusCode">Status code for success responses</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task HandleResultAsync(
        this HttpContext httpContext,
        UnitResult<Error> result,
        int successStatusCode = StatusCodes.Status200OK,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (result.IsSuccess)
        {
            httpContext.Response.StatusCode = successStatusCode;
        }
        else
        {
            await httpContext.SendProblemDetailsAsync(result.Error, cancellationToken);
        }
    }
    
    /// <summary>
    /// Handles a Result{T} as a Created response (201) with location header.
    /// </summary>
    /// <typeparam name="T">The success value type</typeparam>
    /// <param name="httpContext">The HTTP context</param>
    /// <param name="result">The result to handle</param>
    /// <param name="location">Location URI for the created resource</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task HandleCreatedResultAsync<T>(
        this HttpContext httpContext,
        Result<T, Error> result,
        string location,
        CancellationToken cancellationToken = default)
    {
        if (result.IsSuccess)
        {
            httpContext.Response.StatusCode = StatusCodes.Status201Created;
            httpContext.Response.Headers.Location = location;
            await httpContext.Response.WriteAsJsonAsync(result.Value, cancellationToken);
        }
        else
        {
            await httpContext.SendProblemDetailsAsync(result.Error, cancellationToken);
        }
    }
    
    /// <summary>
    /// Handles a Result as a No Content response (204).
    /// </summary>
    /// <param name="httpContext">The HTTP context</param>
    /// <param name="result">The result to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task HandleNoContentResultAsync(
        this HttpContext httpContext,
        UnitResult<Error> result,
        CancellationToken cancellationToken = default)
    {
        if (result.IsSuccess)
        {
            httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
        }
        else
        {
            await httpContext.SendProblemDetailsAsync(result.Error, cancellationToken);
        }
    }
}