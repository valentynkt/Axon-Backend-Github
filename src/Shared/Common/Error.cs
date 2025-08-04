namespace Axon.Shared.Common;

/// <summary>
/// Represents an error that can occur during operation execution
/// </summary>
public sealed record Error
{
    public string Code { get; }
    public string Message { get; }
    public ErrorType Type { get; }
    public Exception? InnerException { get; }

    private Error(string code, string message, ErrorType type, Exception? innerException = null)
    {
        Code = code;
        Message = message;
        Type = type;
        InnerException = innerException;
    }

    public static Error Validation(string message, string code = "VALIDATION_ERROR") =>
        new(code, message, ErrorType.Validation);

    public static Error NotFound(string message, string code = "NOT_FOUND") =>
        new(code, message, ErrorType.NotFound);

    public static Error Conflict(string message, string code = "CONFLICT") =>
        new(code, message, ErrorType.Conflict);

    public static Error InternalError(string message, string code = "INTERNAL_ERROR", Exception? innerException = null) =>
        new(code, message, ErrorType.InternalError, innerException);

    public static Error ExternalService(string message, string code = "EXTERNAL_SERVICE_ERROR", Exception? innerException = null) =>
        new(code, message, ErrorType.ExternalService, innerException);

    public static Error Unauthorized(string message, string code = "UNAUTHORIZED") =>
        new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string message, string code = "FORBIDDEN") =>
        new(code, message, ErrorType.Forbidden);

    public static Error Persistence(string message, string code = "PERSISTENCE_ERROR") =>
        new(code, message, ErrorType.Persistence);

    public override string ToString() => $"[{Type}] {Code}: {Message}";
}

/// <summary>
/// Represents the type of error that occurred
/// </summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    InternalError,
    ExternalService,
    Unauthorized,
    Forbidden,
    Persistence
}