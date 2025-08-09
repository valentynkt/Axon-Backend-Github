using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Options;
using BuildingBlocks.Core.Functional.Validation;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Core.Functional.Extensions;

/// <summary>
/// Bridge extensions for seamless Result ⇄ Option conversions
/// </summary>
public static class ConversionExtensions
{
    #region Result<T> Extensions

    /// <summary>
    /// Convert Result to Option (Success → Some, Failure → None)
    /// </summary>
    public static Option<T> ToOption<T>(this Result<T> result)
    {
        return result.IsSuccess ? Option<T>.Some(result.Value) : Option<T>.None();
    }

    /// <summary>
    /// Convert Result to Option with error callback
    /// </summary>
    public static Option<T> ToOption<T>(this Result<T> result, Action<Error> onError)
    {
        ArgumentNullException.ThrowIfNull(onError);
        
        if (result.IsFailure)
            onError(result.Error);
            
        return result.ToOption();
    }

    /// <summary>
    /// Convert Result to Option with error logging
    /// </summary>
    public static Option<T> ToOption<T>(this Result<T> result, ILogger logger, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        
        if (result.IsFailure)
            logger.LogWarning("Result conversion to Option failed: {Message} - {Error}", 
                message ?? "Conversion", result.Error);
            
        return result.ToOption();
    }

    #endregion

    #region Option<T> Extensions

    /// <summary>
    /// Convert Option to Result with default error
    /// </summary>
    public static Result<T> ToResult<T>(this Option<T> option, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return option.IsSome 
            ? Result<T>.Success(option.Value) 
            : Result<T>.Failure(error);
    }

    /// <summary>
    /// Convert Option to Result with error message
    /// </summary>
    public static Result<T> ToResult<T>(this Option<T> option, string errorMessage)
    {
        return option.ToResult(Error.Validation(errorMessage));
    }

    /// <summary>
    /// Convert Option to Result with error factory
    /// </summary>
    public static Result<T> ToResult<T>(this Option<T> option, Func<Error> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(errorFactory);
        return option.IsSome 
            ? Result<T>.Success(option.Value) 
            : Result<T>.Failure(errorFactory());
    }

    /// <summary>
    /// Convert Option to Result with contextual error
    /// </summary>
    public static Result<T> ToResult<T, TContext>(
        this Option<T> option, 
        TContext context, 
        Func<TContext, Error> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(errorFactory);
        return option.IsSome 
            ? Result<T>.Success(option.Value) 
            : Result<T>.Failure(errorFactory(context));
    }

    /// <summary>
    /// Convert Option to Result with NotFound error for entities
    /// </summary>
    public static Result<T> ToResultNotFound<T>(
        this Option<T> option, 
        string entityName, 
        object? identifier = null)
    {
        var message = identifier != null 
            ? $"{entityName} with identifier '{identifier}' was not found"
            : $"{entityName} was not found";
            
        return option.ToResult(Error.NotFound(message));
    }

    #endregion

    #region Async Extensions

    /// <summary>
    /// Convert Task Result to Task Option
    /// </summary>
    public static async Task<Option<T>> ToOptionAsync<T>(this Task<Result<T>> resultTask)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        var result = await resultTask.ConfigureAwait(false);
        return result.ToOption();
    }

    /// <summary>
    /// Convert Task Option to Task Result
    /// </summary>
    public static async Task<Result<T>> ToResultAsync<T>(this Task<Option<T>> optionTask, Error error)
    {
        ArgumentNullException.ThrowIfNull(optionTask);
        ArgumentNullException.ThrowIfNull(error);
        
        var option = await optionTask.ConfigureAwait(false);
        return option.ToResult(error);
    }

    /// <summary>
    /// Convert Task Option to Task Result with error factory
    /// </summary>
    public static async Task<Result<T>> ToResultAsync<T>(
        this Task<Option<T>> optionTask, 
        Func<Error> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(optionTask);
        ArgumentNullException.ThrowIfNull(errorFactory);
        
        var option = await optionTask.ConfigureAwait(false);
        return option.ToResult(errorFactory);
    }

    #endregion

    #region Validation Extensions

    /// <summary>
    /// Convert Result to Validation
    /// </summary>
    public static Validation<T> ToValidation<T>(this Result<T> result)
    {
        return result.IsSuccess 
            ? Validation<T>.Valid(result.Value)
            : Validation<T>.Invalid(result.Error);
    }

    /// <summary>
    /// Convert Option to Validation
    /// </summary>
    public static Validation<T> ToValidation<T>(this Option<T> option, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return option.IsSome 
            ? Validation<T>.Valid(option.Value)
            : Validation<T>.Invalid(error);
    }

    #endregion

    #region Collection Extensions

    /// <summary>
    /// Convert sequence of Results to Option of sequence (fail-fast)
    /// </summary>
    public static Option<IEnumerable<T>> Sequence<T>(this IEnumerable<Result<T>> results)
    {
        var list = new List<T>();
        foreach (var result in results)
        {
            if (result.IsFailure)
                return Option<IEnumerable<T>>.None();
            list.Add(result.Value);
        }
        return Option<IEnumerable<T>>.Some(list);
    }

    /// <summary>
    /// Convert sequence of Options to Option of sequence (fail-fast)
    /// </summary>
    public static Option<IEnumerable<T>> Sequence<T>(this IEnumerable<Option<T>> options)
    {
        var list = new List<T>();
        foreach (var option in options)
        {
            if (option.IsNone)
                return Option<IEnumerable<T>>.None();
            list.Add(option.Value);
        }
        return Option<IEnumerable<T>>.Some(list);
    }

    /// <summary>
    /// Traverse with Result (map then sequence)
    /// </summary>
    public static Option<IEnumerable<TResult>> Traverse<T, TResult>(
        this IEnumerable<T> source, 
        Func<T, Result<TResult>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return source.Select(mapper).Sequence();
    }

    /// <summary>
    /// Traverse with Option (map then sequence)
    /// </summary>
    public static Option<IEnumerable<TResult>> Traverse<T, TResult>(
        this IEnumerable<T> source, 
        Func<T, Option<TResult>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return source.Select(mapper).Sequence();
    }

    #endregion
}