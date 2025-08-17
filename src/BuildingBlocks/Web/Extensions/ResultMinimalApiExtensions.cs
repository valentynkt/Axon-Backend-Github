using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Web.ProblemDetails;
using Microsoft.AspNetCore.Http;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace BuildingBlocks.Web.Extensions;

/// <summary>
/// Extensions for converting Result types to IResult for Minimal APIs with automatic Problem Details.
/// </summary>
public static class ResultMinimalApiExtensions
{
    /// <summary>
    /// Converts Result{T} to IResult for Minimal APIs.
    /// </summary>
    /// <typeparam name="T">The success value type</typeparam>
    /// <param name="result">The result to convert</param>
    /// <param name="httpContext">HttpContext for instance and trace ID</param>
    /// <returns>IResult with success value or Problem Details</returns>
    public static IResult ToResult<T>(
        this Result<T> result,
        HttpContext httpContext)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }
        
        var problemDetails = result.Error.ToProblemDetails(
            instance: httpContext.Request.Path,
            traceId: httpContext.TraceIdentifier);
            
        return Results.Problem(problemDetails);
    }
    
    /// <summary>
    /// Converts Result to IResult with custom success response factory.
    /// </summary>
    /// <param name="result">The result to convert</param>
    /// <param name="successFactory">Factory to create success IResult</param>
    /// <param name="httpContext">HttpContext for instance and trace ID</param>
    /// <returns>IResult from factory or Problem Details</returns>
    public static IResult ToResult(
        this Result result,
        Func<IResult> successFactory,
        HttpContext httpContext)
    {
        if (result.IsSuccess)
        {
            return successFactory();
        }
        
        var problemDetails = result.Error.ToProblemDetails(
            instance: httpContext.Request.Path,
            traceId: httpContext.TraceIdentifier);
            
        return Results.Problem(problemDetails);
    }
    
    /// <summary>
    /// Converts Result{T} to Created result (201) with location.
    /// </summary>
    /// <typeparam name="T">The success value type</typeparam>
    /// <param name="result">The result to convert</param>
    /// <param name="uri">The location URI for the created resource</param>
    /// <param name="httpContext">HttpContext for instance and trace ID</param>
    /// <returns>Created result or Problem Details</returns>
    public static IResult ToCreatedResult<T>(
        this Result<T> result,
        string uri,
        HttpContext httpContext)
    {
        if (result.IsSuccess)
        {
            return Results.Created(uri, result.Value);
        }
        
        var problemDetails = result.Error.ToProblemDetails(
            instance: httpContext.Request.Path,
            traceId: httpContext.TraceIdentifier);
            
        return Results.Problem(problemDetails);
    }
    
    /// <summary>
    /// Converts Result{T} to Accepted result (202).
    /// </summary>
    /// <typeparam name="T">The success value type</typeparam>
    /// <param name="result">The result to convert</param>
    /// <param name="uri">Optional URI to check operation status</param>
    /// <param name="httpContext">HttpContext for instance and trace ID</param>
    /// <returns>Accepted result or Problem Details</returns>
    public static IResult ToAcceptedResult<T>(
        this Result<T> result,
        string? uri,
        HttpContext httpContext)
    {
        if (result.IsSuccess)
        {
            return uri != null 
                ? Results.Accepted(uri, result.Value)
                : Results.Accepted(value: result.Value);
        }
        
        var problemDetails = result.Error.ToProblemDetails(
            instance: httpContext.Request.Path,
            traceId: httpContext.TraceIdentifier);
            
        return Results.Problem(problemDetails);
    }
    
    /// <summary>
    /// Converts Result to NoContent result (204).
    /// </summary>
    /// <param name="result">The result to convert</param>
    /// <param name="httpContext">HttpContext for instance and trace ID</param>
    /// <returns>NoContent result or Problem Details</returns>
    public static IResult ToNoContentResult(
        this Result result,
        HttpContext httpContext)
    {
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }
        
        var problemDetails = result.Error.ToProblemDetails(
            instance: httpContext.Request.Path,
            traceId: httpContext.TraceIdentifier);
            
        return Results.Problem(problemDetails);
    }
}