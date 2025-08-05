using Axon.Shared.Common;
using Microsoft.AspNetCore.Mvc;

namespace Axon.Api.Common.ErrorHandling;

/// <summary>
/// Maps domain errors to HTTP responses
/// </summary>
public sealed class ErrorMapper : IErrorMapper
{

    /// <inheritdoc />
    public int MapToStatusCode(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        
        return error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.ExternalService => StatusCodes.Status502BadGateway,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.InternalError => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    /// <inheritdoc />
    public object MapToProblemDetails(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        
        var statusCode = MapToStatusCode(error);
        
        var title = error.Type switch
        {
            ErrorType.Validation => "Validation Error",
            ErrorType.NotFound => "Not Found",
            ErrorType.Conflict => "Conflict",
            ErrorType.ExternalService => "External Service Error",
            ErrorType.Unauthorized => "Unauthorized",
            ErrorType.Forbidden => "Forbidden",
            ErrorType.InternalError => "Internal Server Error",
            _ => "Internal Server Error"
        };

        var detail = GetErrorDetailMessage(error, statusCode);

        return new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = statusCode,
            Type = $"https://httpstatuses.com/{statusCode}"
        };
    }

    private static string GetErrorDetailMessage(Error error, int statusCode)
    {
        return ShouldSanitizeErrorMessage(error.Type, statusCode) 
            ? "An unexpected error occurred" 
            : error.Message;
    }

    private static bool ShouldSanitizeErrorMessage(ErrorType errorType, int statusCode)
    {
        return errorType == ErrorType.InternalError && 
               statusCode == StatusCodes.Status500InternalServerError;
    }
}