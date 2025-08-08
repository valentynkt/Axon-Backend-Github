using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Web.ProblemDetails;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BuildingBlocks.Web.Extensions;

/// <summary>
/// Extensions for converting Result types to ActionResult with automatic Problem Details generation.
/// </summary>
public static class ResultActionResultExtensions
{
    /// <summary>
    /// Converts Result{T} to ActionResult{T} with automatic Problem Details for failures.
    /// </summary>
    /// <typeparam name="T">The success value type</typeparam>
    /// <param name="result">The result to convert</param>
    /// <param name="httpContext">Optional HttpContext for instance and trace ID</param>
    /// <returns>ActionResult with success value or Problem Details</returns>
    public static ActionResult<T> ToActionResult<T>(
        this Result<T> result,
        HttpContext? httpContext = null)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(result.Value);
        }
        
        var problemDetails = result.Error.ToProblemDetails(
            instance: httpContext?.Request.Path,
            traceId: httpContext?.TraceIdentifier);
            
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }
    
    /// <summary>
    /// Converts Result to IActionResult with custom success status code.
    /// </summary>
    /// <param name="result">The result to convert</param>
    /// <param name="successStatusCode">Status code to return on success</param>
    /// <param name="httpContext">Optional HttpContext for instance and trace ID</param>
    /// <returns>ActionResult with success status or Problem Details</returns>
    public static IActionResult ToActionResult(
        this Result result,
        int successStatusCode = StatusCodes.Status200OK,
        HttpContext? httpContext = null)
    {
        if (result.IsSuccess)
        {
            return new StatusCodeResult(successStatusCode);
        }
        
        var problemDetails = result.Error.ToProblemDetails(
            instance: httpContext?.Request.Path,
            traceId: httpContext?.TraceIdentifier);
            
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }
    
    /// <summary>
    /// Converts Result{T} to created response (201) with location header.
    /// </summary>
    /// <typeparam name="T">The success value type</typeparam>
    /// <param name="result">The result to convert</param>
    /// <param name="location">The location URI for the created resource</param>
    /// <param name="httpContext">Optional HttpContext for instance and trace ID</param>
    /// <returns>CreatedResult with value or Problem Details</returns>
    public static IActionResult ToCreatedResult<T>(
        this Result<T> result,
        string location,
        HttpContext? httpContext = null)
    {
        if (result.IsSuccess)
        {
            return new CreatedResult(location, result.Value);
        }
        
        var problemDetails = result.Error.ToProblemDetails(
            instance: httpContext?.Request.Path,
            traceId: httpContext?.TraceIdentifier);
            
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }
    
    /// <summary>
    /// Converts Result{T} to accepted response (202) for async operations.
    /// </summary>
    /// <typeparam name="T">The success value type</typeparam>
    /// <param name="result">The result to convert</param>
    /// <param name="location">Optional location URI to check operation status</param>
    /// <param name="httpContext">Optional HttpContext for instance and trace ID</param>
    /// <returns>AcceptedResult with value or Problem Details</returns>
    public static IActionResult ToAcceptedResult<T>(
        this Result<T> result,
        string? location = null,
        HttpContext? httpContext = null)
    {
        if (result.IsSuccess)
        {
            return location != null 
                ? new AcceptedResult(location, result.Value)
                : new AcceptedResult(null, result.Value);
        }
        
        var problemDetails = result.Error.ToProblemDetails(
            instance: httpContext?.Request.Path,
            traceId: httpContext?.TraceIdentifier);
            
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }
    
    /// <summary>
    /// Converts Result to no content response (204).
    /// </summary>
    /// <param name="result">The result to convert</param>
    /// <param name="httpContext">Optional HttpContext for instance and trace ID</param>
    /// <returns>NoContentResult or Problem Details</returns>
    public static IActionResult ToNoContentResult(
        this Result result,
        HttpContext? httpContext = null)
    {
        if (result.IsSuccess)
        {
            return new NoContentResult();
        }
        
        var problemDetails = result.Error.ToProblemDetails(
            instance: httpContext?.Request.Path,
            traceId: httpContext?.TraceIdentifier);
            
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }
}