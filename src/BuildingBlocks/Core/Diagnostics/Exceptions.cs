using System.Net;

namespace BuildingBlocks.Core.Diagnostics;

/// <summary>
/// Base application exception
/// </summary>
public class AppException : System.Exception
{
    public AppException(string message) : base(message) { }
    public AppException(string message, System.Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown for bad requests
/// </summary>
public class BadRequestException : AppException
{
    public BadRequestException(string message) : base(message) { }
    public BadRequestException(string message, System.Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when a resource is not found
/// </summary>
public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string message, System.Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when there's a conflict
/// </summary>
public class ConflictException : AppException
{
    public ConflictException(string message) : base(message) { }
    public ConflictException(string message, System.Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Enhanced validation exception with HTTP status code
/// </summary>
public class ValidationException : AppException
{
    public HttpStatusCode StatusCode { get; }

    public ValidationException(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest) : base(message) 
    { 
        StatusCode = statusCode;
    }
    
    public ValidationException(string message, System.Exception innerException, HttpStatusCode statusCode = HttpStatusCode.BadRequest) : base(message, innerException) 
    { 
        StatusCode = statusCode;
    }
}