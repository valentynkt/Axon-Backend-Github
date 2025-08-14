using System.Net;
using BuildingBlocks.Core.Functional;

namespace BuildingBlocks.Core.Diagnostics;

/// <summary>
/// Base application exception with Result pattern integration.
/// Provides seamless conversion between exceptions and Result pattern errors.
/// </summary>
public class AppException : Exception
{
    /// <summary>
    /// The Error associated with this exception for Result pattern integration.
    /// </summary>
    public Error? Error { get; }

    public AppException(string message) : base(message) { }
    
    public AppException(string message, Exception innerException) : base(message, innerException) { }
    
    /// <summary>
    /// Creates an AppException from a Result Error.
    /// Enables conversion from Result pattern to exception-based handling.
    /// </summary>
    /// <param name="error">The error to convert to an exception</param>
    public AppException(Error error) : base(error.Message, error.InnerException)
    {
        Error = error;
        
        // Store error in Data for round-trip conversion
        Data["AxonError"] = error;
    }

    /// <summary>
    /// Converts this exception to a Result Error.
    /// Enables conversion from exception to Result pattern.
    /// </summary>
    /// <returns>An Error representing this exception</returns>
    public virtual Error ToError()
    {
        // If we already have an Error, return it
        if (Error != null)
            return Error;

        // If there's an Error stored in Data, use it
        if (Data.Contains("AxonError") && Data["AxonError"] is Error storedError)
            return storedError;

        // Create a new Error from this exception
        return Error.FromException(this);
    }
}

/// <summary>
/// Exception thrown for bad requests with Result pattern integration.
/// </summary>
public class BadRequestException : AppException
{
    public BadRequestException(string message) : base(message) { }
    
    public BadRequestException(string message, Exception innerException) : base(message, innerException) { }
    
    public BadRequestException(Error error) : base(error) { }

    public override Error ToError()
    {
        if (Error != null)
            return Error;

        return Error.BusinessRule(Message, "BAD_REQUEST");
    }
}

/// <summary>
/// Exception thrown when a resource is not found with Result pattern integration.
/// </summary>
public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message) { }
    
    public NotFoundException(string message, Exception innerException) : base(message, innerException) { }
    
    public NotFoundException(Error error) : base(error) { }

    public override Error ToError()
    {
        if (Error != null)
            return Error;

        return Error.NotFound(Message, "NOT_FOUND");
    }
}

/// <summary>
/// Exception thrown when there's a conflict with Result pattern integration.
/// </summary>
public class ConflictException : AppException
{
    public ConflictException(string message) : base(message) { }
    
    public ConflictException(string message, Exception innerException) : base(message, innerException) { }
    
    public ConflictException(Error error) : base(error) { }

    public override Error ToError()
    {
        if (Error != null)
            return Error;

        return Error.Conflict(Message, "CONFLICT");
    }
}

/// <summary>
/// Enhanced validation exception with HTTP status code and Result pattern integration.
/// </summary>
public class ValidationException : AppException
{
    public HttpStatusCode StatusCode { get; }

    public ValidationException(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest) : base(message) 
    { 
        StatusCode = statusCode;
    }
    
    public ValidationException(string message, Exception innerException, HttpStatusCode statusCode = HttpStatusCode.BadRequest) : base(message, innerException) 
    { 
        StatusCode = statusCode;
    }

    public ValidationException(Error error, HttpStatusCode statusCode = HttpStatusCode.BadRequest) : base(error)
    {
        StatusCode = statusCode;
    }

    public override Error ToError()
    {
        if (Error != null)
            return Error;

        return Error.Validation(Message, "VALIDATION_ERROR");
    }
}

/// <summary>
/// Extension methods for exception-Result pattern integration.
/// </summary>
public static class ExceptionExtensions
{
    /// <summary>
    /// Converts any exception to an Error, with special handling for AppExceptions.
    /// </summary>
    /// <param name="exception">The exception to convert</param>
    /// <returns>An Error representing the exception</returns>
    public static Error ToError(this Exception exception)
    {
        return exception switch
        {
            AppException appException => appException.ToError(),
            _ => Error.FromException(exception)
        };
    }

    /// <summary>
    /// Determines if an exception contains Result pattern error information.
    /// </summary>
    /// <param name="exception">The exception to check</param>
    /// <returns>True if the exception contains Result pattern error data</returns>
    public static bool HasResultError(this Exception exception)
    {
        return exception is AppException { Error: not null } ||
               exception.Data.Contains("AxonError");
    }

    /// <summary>
    /// Extracts the Result Error from an exception if present.
    /// </summary>
    /// <param name="exception">The exception to extract from</param>
    /// <returns>The Error if present, null otherwise</returns>
    public static Error? GetResultError(this Exception exception)
    {
        if (exception is AppException { Error: not null } appException)
            return appException.Error;

        if (exception.Data.Contains("AxonError") && exception.Data["AxonError"] is Error error)
            return error;

        return null;
    }
}