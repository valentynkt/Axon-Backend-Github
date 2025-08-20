using BuildingBlocks.Core.Functional.Results;
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
    protected static Result<T> ValidationError<T>(string code, string message)
    {
        var error = BuildingBlocks.Core.Diagnostics.Errors.Error.Validation(code, message);
        return Result<T>.Failure(error);
    }

    /// <summary>
    /// Creates a validation error result for missing required values.
    /// </summary>
    protected static Result<T> RequiredValueMissing<T>(string fieldName)
    {
        return ValidationError<T>(
            "REQUIRED_VALUE_MISSING",
            $"Required field '{fieldName}' is missing or invalid");
    }
}