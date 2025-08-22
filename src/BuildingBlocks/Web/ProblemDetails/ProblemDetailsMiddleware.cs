using BuildingBlocks.Application.Exceptions;
using BuildingBlocks.Core.Diagnostics.Exceptions;
using BuildingBlocks.Web.ProblemDetails;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Web.ProblemDetails;

/// <summary>
/// Middleware to handle unhandled exceptions and convert them to RFC 7807 Problem Details.
/// </summary>
public class ProblemDetailsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsMiddleware> _logger;
    private readonly IProblemDetailsService _problemDetailsService;
    
    public ProblemDetailsMiddleware(
        RequestDelegate next,
        ILogger<ProblemDetailsMiddleware> logger,
        IProblemDetailsService problemDetailsService)
    {
        _next = next;
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }
    
    /// <summary>
    /// Processes the HTTP request and handles any unhandled exceptions.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            await HandleValidationExceptionAsync(context, ex);
        }
        catch (BusinessRuleException ex)
        {
            await HandleBusinessRuleExceptionAsync(context, ex);
        }
        catch (DomainException ex)
        {
            await HandleDomainExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            await HandleGenericExceptionAsync(context, ex);
        }
    }
    
    private async Task HandleDomainExceptionAsync(
        HttpContext context, 
        DomainException exception)
    {
        _logger.LogWarning(exception,
            "Domain exception occurred: {ErrorCode} - {Message}",
            exception.Error.Code,
            exception.Error.Message);
            
        var problemDetails = exception.Error.ToProblemDetails(
            instance: context.Request.Path,
            traceId: context.TraceIdentifier);
            
        context.Response.StatusCode = problemDetails.Status ?? 500;
        
        await _problemDetailsService.WriteAsync(new()
        {
            HttpContext = context,
            ProblemDetails = problemDetails
        });
    }
    
    private async Task HandleValidationExceptionAsync(
        HttpContext context,
        ValidationException exception)
    {
        _logger.LogWarning(exception,
            "Validation exception occurred with {ErrorCount} errors",
            exception.Errors.Count);
            
        var validationDetails = exception.Errors.ToValidationProblemDetails(
            instance: context.Request.Path,
            traceId: context.TraceIdentifier);
            
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        
        await _problemDetailsService.WriteAsync(new()
        {
            HttpContext = context,
            ProblemDetails = validationDetails
        });
    }
    
    private async Task HandleBusinessRuleExceptionAsync(
        HttpContext context,
        BusinessRuleException exception)
    {
        _logger.LogWarning(exception,
            "Business rule violation: {ErrorCode} - {Message}",
            exception.Error.Code,
            exception.Error.Message);
            
        var problemDetails = exception.Error.ToProblemDetails(
            instance: context.Request.Path,
            traceId: context.TraceIdentifier);
            
        context.Response.StatusCode = problemDetails.Status ?? 422;
        
        await _problemDetailsService.WriteAsync(new()
        {
            HttpContext = context,
            ProblemDetails = problemDetails
        });
    }
    
    private async Task HandleGenericExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        _logger.LogError(exception,
            "Unhandled exception occurred: {Message}",
            exception.Message);
            
        // Create a generic internal server error
        var error = Error.Internal(
            "An unexpected error occurred.",
            "INTERNAL_SERVER_ERROR")
            .WithSource(exception.GetType().Name)
            .WithMetadata("exceptionType", exception.GetType().FullName ?? exception.GetType().Name);
            
        var problemDetails = error.ToProblemDetails(
            instance: context.Request.Path,
            traceId: context.TraceIdentifier);
            
        // Don't expose internal details in production
        #if !DEBUG
        problemDetails.Detail = "An unexpected error occurred.";
        problemDetails.Extensions.Remove("source");
        problemDetails.Extensions.Remove("exceptionType");
        #endif
        
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        
        await _problemDetailsService.WriteAsync(new()
        {
            HttpContext = context,
            ProblemDetails = problemDetails
        });
    }
}