using Axon.Shared.Common;

namespace Axon.Tests.Shared.Builders;

/// <summary>
/// Builder pattern for creating Result{T} instances in tests with fluent API
/// </summary>
public sealed class ResultBuilder<T>
{
    private bool _isSuccess = true;
    private T? _value = default;
    private Error? _error = null;

    private ResultBuilder() { }

    /// <summary>
    /// Creates a new ResultBuilder for successful results
    /// </summary>
    public static ResultBuilder<T> Success() => new() { _isSuccess = true };

    /// <summary>
    /// Creates a new ResultBuilder for failed results
    /// </summary>
    public static ResultBuilder<T> Failure() => new() { _isSuccess = false };

    /// <summary>
    /// Sets the value for successful results
    /// </summary>
    public ResultBuilder<T> WithValue(T value)
    {
        _value = value;
        _isSuccess = true;
        return this;
    }

    /// <summary>
    /// Sets a validation error
    /// </summary>
    public ResultBuilder<T> WithValidationError(string message, string? code = null)
    {
        _error = Error.Validation(message, code ?? "VALIDATION_ERROR");
        _isSuccess = false;
        return this;
    }

    /// <summary>
    /// Sets a not found error
    /// </summary>
    public ResultBuilder<T> WithNotFoundError(string message, string? code = null)
    {
        _error = Error.NotFound(message, code ?? "NOT_FOUND");
        _isSuccess = false;
        return this;
    }

    /// <summary>
    /// Sets a conflict error
    /// </summary>
    public ResultBuilder<T> WithConflictError(string message, string? code = null)
    {
        _error = Error.Conflict(message, code ?? "CONFLICT");
        _isSuccess = false;
        return this;
    }

    /// <summary>
    /// Sets an internal error
    /// </summary>
    public ResultBuilder<T> WithInternalError(string message, string? code = null, Exception? innerException = null)
    {
        _error = Error.InternalError(message, code ?? "INTERNAL_ERROR", innerException);
        _isSuccess = false;
        return this;
    }

    /// <summary>
    /// Sets an external service error
    /// </summary>
    public ResultBuilder<T> WithExternalServiceError(string message, string? code = null, Exception? innerException = null)
    {
        _error = Error.ExternalService(message, code ?? "EXTERNAL_SERVICE_ERROR", innerException);
        _isSuccess = false;
        return this;
    }

    /// <summary>
    /// Sets an unauthorized error
    /// </summary>
    public ResultBuilder<T> WithUnauthorizedError(string message, string? code = null)
    {
        _error = Error.Unauthorized(message, code ?? "UNAUTHORIZED");
        _isSuccess = false;
        return this;
    }

    /// <summary>
    /// Sets a forbidden error
    /// </summary>
    public ResultBuilder<T> WithForbiddenError(string message, string? code = null)
    {
        _error = Error.Forbidden(message, code ?? "FORBIDDEN");
        _isSuccess = false;
        return this;
    }

    /// <summary>
    /// Sets a custom error
    /// </summary>
    public ResultBuilder<T> WithError(Error error)
    {
        _error = error;
        _isSuccess = false;
        return this;
    }

    /// <summary>
    /// Builds the Result{T} instance
    /// </summary>
    public Result<T> Build()
    {
        if (_isSuccess)
        {
            return _value is not null ? Result<T>.Success(_value) : 
                   throw new InvalidOperationException("Cannot create successful result without a value. Use WithValue() method.");
        }

        return _error is not null ? Result<T>.Failure(_error) : 
               throw new InvalidOperationException("Cannot create failed result without an error. Use WithError() or one of the With*Error() methods.");
    }

    /// <summary>
    /// Implicit conversion to Result{T}
    /// </summary>
    public static implicit operator Result<T>(ResultBuilder<T> builder) => builder.Build();
}

/// <summary>
/// Builder pattern for creating non-generic Result instances
/// </summary>
public sealed class ResultBuilder
{
    private bool _isSuccess = true;
    private Error? _error = null;

    private ResultBuilder() { }

    /// <summary>
    /// Creates a new ResultBuilder for successful results
    /// </summary>
    public static ResultBuilder Success() => new() { _isSuccess = true };

    /// <summary>
    /// Creates a new ResultBuilder for failed results
    /// </summary>
    public static ResultBuilder Failure() => new() { _isSuccess = false };

    /// <summary>
    /// Sets a validation error
    /// </summary>
    public ResultBuilder WithValidationError(string message, string? code = null)
    {
        _error = Error.Validation(message, code ?? "VALIDATION_ERROR");
        _isSuccess = false;
        return this;
    }

    /// <summary>
    /// Sets a not found error
    /// </summary>
    public ResultBuilder WithNotFoundError(string message, string? code = null)
    {
        _error = Error.NotFound(message, code ?? "NOT_FOUND");
        _isSuccess = false;
        return this;
    }

    /// <summary>
    /// Sets a conflict error
    /// </summary>
    public ResultBuilder WithConflictError(string message, string? code = null)
    {
        _error = Error.Conflict(message, code ?? "CONFLICT");
        _isSuccess = false;
        return this;
    }

    /// <summary>
    /// Sets an internal error
    /// </summary>
    public ResultBuilder WithInternalError(string message, string? code = null, Exception? innerException = null)
    {
        _error = Error.InternalError(message, code ?? "INTERNAL_ERROR", innerException);
        _isSuccess = false;
        return this;
    }

    /// <summary>
    /// Sets an external service error
    /// </summary>
    public ResultBuilder WithExternalServiceError(string message, string? code = null, Exception? innerException = null)
    {
        _error = Error.ExternalService(message, code ?? "EXTERNAL_SERVICE_ERROR", innerException);
        _isSuccess = false;
        return this;
    }

    /// <summary>
    /// Sets an unauthorized error
    /// </summary>
    public ResultBuilder WithUnauthorizedError(string message, string? code = null)
    {
        _error = Error.Unauthorized(message, code ?? "UNAUTHORIZED");
        _isSuccess = false;
        return this;
    }

    /// <summary>
    /// Sets a forbidden error
    /// </summary>
    public ResultBuilder WithForbiddenError(string message, string? code = null)
    {
        _error = Error.Forbidden(message, code ?? "FORBIDDEN");
        _isSuccess = false;
        return this;
    }

    /// <summary>
    /// Sets a custom error
    /// </summary>
    public ResultBuilder WithError(Error error)
    {
        _error = error;
        _isSuccess = false;
        return this;
    }

    /// <summary>
    /// Builds the Result instance
    /// </summary>
    public Result Build()
    {
        if (_isSuccess)
        {
            return Result.Success();
        }

        return _error is not null ? Result.Failure(_error) : 
               throw new InvalidOperationException("Cannot create failed result without an error. Use WithError() or one of the With*Error() methods.");
    }

    /// <summary>
    /// Implicit conversion to Result
    /// </summary>
    public static implicit operator Result(ResultBuilder builder) => builder.Build();
}

/// <summary>
/// Static factory methods for ResultBuilder with common types
/// </summary>
public static class ResultBuilders
{
    /// <summary>
    /// Creates a ResultBuilder for string results
    /// </summary>
    public static ResultBuilder<string> String() => ResultBuilder<string>.Success();

    /// <summary>
    /// Creates a ResultBuilder for int results
    /// </summary>
    public static ResultBuilder<int> Int() => ResultBuilder<int>.Success();

    /// <summary>
    /// Creates a ResultBuilder for bool results
    /// </summary>
    public static ResultBuilder<bool> Bool() => ResultBuilder<bool>.Success();

    /// <summary>
    /// Creates a ResultBuilder for Guid results
    /// </summary>
    public static ResultBuilder<Guid> Guid() => ResultBuilder<Guid>.Success();

    /// <summary>
    /// Creates a ResultBuilder for DateTime results
    /// </summary>
    public static ResultBuilder<DateTime> DateTime() => ResultBuilder<DateTime>.Success();

    /// <summary>
    /// Creates a ResultBuilder for any type T
    /// </summary>
    public static ResultBuilder<T> For<T>() => ResultBuilder<T>.Success();

    /// <summary>
    /// Creates a non-generic ResultBuilder
    /// </summary>
    public static ResultBuilder NonGeneric() => ResultBuilder.Success();
}