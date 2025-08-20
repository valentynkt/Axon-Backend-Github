using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Web.Mappers;

/// <summary>
/// Base class for mappers providing common functionality.
/// </summary>
public abstract class BaseMapper
{
    protected ILogger Logger { get; }

    protected BaseMapper(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a validation error result for mapping failures.
    /// </summary>
    protected static Result<T, Error> ValidationError<T>(string message, string code = "VALIDATION_ERROR")
    {
        var error = Error.Validation(message, code);
        return Result.Failure<T, Error>(error);
    }

    /// <summary>
    /// Creates a validation error result for missing required values.
    /// </summary>
    protected static Result<T, Error> RequiredValueMissing<T>(string fieldName)
    {
        return ValidationError<T>(
            $"Required field '{fieldName}' is missing or invalid",
            "REQUIRED_VALUE_MISSING");
    }
}