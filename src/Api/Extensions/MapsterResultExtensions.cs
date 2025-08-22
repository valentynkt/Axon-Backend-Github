using Mapster;
using Axon.BuildingBlocks.Core.Functional.Results;
using Axon.BuildingBlocks.Core.Functional.Errors;

namespace Axon.Api.Extensions;

/// <summary>
/// Extension methods to integrate Mapster with the Result pattern
/// </summary>
public static class MapsterResultExtensions
{
    /// <summary>
    /// Adapts source object to destination type and wraps result in Result pattern
    /// </summary>
    /// <typeparam name="TDestination">Destination type</typeparam>
    /// <param name="source">Source object</param>
    /// <returns>Result containing mapped object or error</returns>
    public static Result<TDestination, Error> AdaptToResult<TDestination>(this object source)
    {
        try
        {
            var result = source.Adapt<TDestination>();
            return Result.Success<TDestination, Error>(result);
        }
        catch (Exception ex)
        {
            return Result.Failure<TDestination, Error>(
                Error.Unexpected($"Mapping failed: {ex.Message}", "MAPPING_ERROR"));
        }
    }

    /// <summary>
    /// Adapts source object to destination type with validation
    /// </summary>
    /// <typeparam name="TDestination">Destination type</typeparam>
    /// <param name="source">Source object</param>
    /// <returns>Result containing mapped object or validation error</returns>
    public static Result<TDestination, Error> AdaptWithValidation<TDestination>(this object? source)
    {
        if (source is null)
        {
            return Result.Failure<TDestination, Error>(
                Error.Validation("Source object cannot be null", "NULL_SOURCE"));
        }

        return source.AdaptToResult<TDestination>();
    }
}