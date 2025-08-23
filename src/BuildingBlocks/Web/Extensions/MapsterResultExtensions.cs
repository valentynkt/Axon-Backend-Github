using BuildingBlocks.Core.Diagnostics.Errors;
using Mapster;
using CSharpFunctionalExtensions;

namespace BuildingBlocks.Web.Extensions;

/// <summary>
/// Extension methods to integrate Mapster with the Result pattern
/// </summary>
public static class MapsterResultExtensions
{
    /// <summary>
    /// Safely adapts source object to destination type with error handling
    /// </summary>
    /// <typeparam name="TDestination">Destination type</typeparam>
    /// <param name="source">Source object</param>
    /// <returns>Result containing mapped object or error</returns>
    public static Result<TDestination, Error> AdaptSafely<TDestination>(this object source)
    {
        try
        {
            var result = source.Adapt<TDestination>();
            return Result.Success<TDestination, Error>(result);
        }
        catch (Exception ex)
        {
            return Result.Failure<TDestination, Error>(
                Error.Internal($"Mapping failed from {source?.GetType().Name ?? "null"} to {typeof(TDestination).Name}: {ex.Message}", "MAPPING_ERROR", ex));
        }
    }

    /// <summary>
    /// Adapts source object to destination type with null validation
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

        return source.AdaptSafely<TDestination>();
    }

    /// <summary>
    /// Chain mapping operations with Result pattern
    /// </summary>
    /// <typeparam name="TSource">Source type</typeparam>
    /// <typeparam name="TIntermediate">Intermediate type</typeparam>
    /// <typeparam name="TDestination">Final destination type</typeparam>
    /// <param name="source">Source object</param>
    /// <returns>Result containing final mapped object or first error encountered</returns>
    public static Result<TDestination, Error> AdaptChain<TSource, TIntermediate, TDestination>(
        this TSource source)
        where TSource : notnull
    {
        return source
            .AdaptSafely<TIntermediate>()
            .Bind(intermediate => intermediate?.AdaptSafely<TDestination>() ?? 
                Result.Failure<TDestination, Error>(Error.Validation("Intermediate result is null", "NULL_INTERMEDIATE")));
    }

    /// <summary>
    /// Maps multiple items safely, stopping on first error
    /// </summary>
    /// <typeparam name="TSource">Source type</typeparam>
    /// <typeparam name="TDestination">Destination type</typeparam>
    /// <param name="sources">Source collection</param>
    /// <returns>Result containing mapped collection or first error</returns>
    public static Result<IEnumerable<TDestination>, Error> AdaptMany<TSource, TDestination>(
        this IEnumerable<TSource> sources)
        where TSource : notnull
    {
        try
        {
            var results = sources.Adapt<IEnumerable<TDestination>>();
            return Result.Success<IEnumerable<TDestination>, Error>(results);
        }
        catch (Exception ex)
        {
            return Result.Failure<IEnumerable<TDestination>, Error>(
                Error.Internal($"Batch mapping failed: {ex.Message}", "MAPPING_ERROR", ex));
        }
    }
}