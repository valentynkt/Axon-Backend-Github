using BuildingBlocks.Core.Diagnostics.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BuildingBlocks.Web.ProblemDetails;

/// <summary>
/// Extensions for converting Error instances to RFC 7807 Problem Details.
/// </summary>
public static class ErrorProblemDetailsExtensions
{
    /// <summary>
    /// Converts an Error to a ProblemDetails object following RFC 7807.
    /// </summary>
    /// <param name="error">The error to convert</param>
    /// <param name="instance">The URI reference that identifies the specific occurrence of the problem</param>
    /// <param name="traceId">The trace identifier for correlation</param>
    /// <returns>RFC 7807 compliant ProblemDetails</returns>
    public static Microsoft.AspNetCore.Mvc.ProblemDetails ToProblemDetails(
        this Error error,
        string? instance = null,
        string? traceId = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var statusCode = error.ToHttpStatusCode();
        
        var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = ErrorHttpMapping.GetProblemType(error.Type, statusCode),
            Title = ErrorHttpMapping.GetProblemTitle(statusCode),
            Status = statusCode,
            Detail = error.Message,
            Instance = instance ?? error.CorrelationId
        };
        
        // Add RFC 7807 extensions for enhanced error information
        problemDetails.Extensions["errorCode"] = error.Code;
        problemDetails.Extensions["errorType"] = error.Type.ToString();
        problemDetails.Extensions["severity"] = error.Severity.ToString();
        problemDetails.Extensions["timestamp"] = error.OccurredAt.ToString("O");
        
        if (!string.IsNullOrEmpty(traceId))
            problemDetails.Extensions["traceId"] = traceId;
        
        if (!string.IsNullOrEmpty(error.Source))
            problemDetails.Extensions["source"] = error.Source;
        
        if (!string.IsNullOrEmpty(error.CorrelationId))
            problemDetails.Extensions["correlationId"] = error.CorrelationId;
        
        // Add error metadata as extensions with camelCase keys
        if (error.Metadata != null)
        {
            foreach (var (key, value) in error.Metadata)
            {
                var camelCaseKey = ToCamelCase(key);
                problemDetails.Extensions[camelCaseKey] = value;
            }
        }
        
        return problemDetails;
    }
    
    /// <summary>
    /// Converts multiple validation errors to ValidationProblemDetails.
    /// </summary>
    /// <param name="errors">The collection of validation errors</param>
    /// <param name="instance">The URI reference that identifies the specific occurrence of the problem</param>
    /// <param name="traceId">The trace identifier for correlation</param>
    /// <returns>ValidationProblemDetails with grouped error messages</returns>
    public static ValidationProblemDetails ToValidationProblemDetails(
        this IEnumerable<Error> errors,
        string? instance = null,
        string? traceId = null)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var validationDetails = new ValidationProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Instance = instance
        };
        
        // Group errors by property name if available
        var errorGroups = new Dictionary<string, List<string>>();
        
        foreach (var error in errors)
        {
            var propertyName = error.Metadata?.GetValueOrDefault("PropertyName")?.ToString() 
                ?? error.Metadata?.GetValueOrDefault("propertyName")?.ToString()
                ?? "General";
                
            if (!errorGroups.ContainsKey(propertyName))
                errorGroups[propertyName] = new List<string>();
                
            errorGroups[propertyName].Add(error.Message);
        }
        
        // Convert to the required string[] format
        foreach (var (key, value) in errorGroups)
        {
            validationDetails.Errors[key] = value.ToArray();
        }
        
        // Add common extensions
        if (!string.IsNullOrEmpty(traceId))
            validationDetails.Extensions["traceId"] = traceId;
            
        validationDetails.Extensions["timestamp"] = DateTimeOffset.UtcNow.ToString("O");
        validationDetails.Extensions["errorCount"] = errors.Count();
        
        return validationDetails;
    }
    
    /// <summary>
    /// Converts a string to camelCase.
    /// </summary>
    private static string ToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
            
        if (input.Length == 1)
            return input.ToLowerInvariant();
            
        return char.ToLowerInvariant(input[0]) + input[1..];
    }
}