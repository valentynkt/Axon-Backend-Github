#pragma warning disable CA1031 // Do not catch general exception types - intentional for ToResultAsync methods

namespace BuildingBlocks.Core.Results;

/// <summary>
/// Extension methods for Result types to support async operations and enhanced functionality
/// </summary>
public static class ResultExtensions
{
    #region Task<Result> Extensions

    /// <summary>
    /// Transforms the value if the async result is successful
    /// </summary>
    public static async Task<Result<TOutput>> MapAsync<T, TOutput>(this Task<Result<T>> resultTask, Func<T, TOutput> transform)
    {
        var result = await resultTask;
        return result.Map(transform);
    }

    /// <summary>
    /// Transforms the value if the async result is successful, with async transform
    /// </summary>
    public static async Task<Result<TOutput>> MapAsync<T, TOutput>(this Task<Result<T>> resultTask, Func<T, Task<TOutput>> transform)
    {
        var result = await resultTask;
        if (result.IsFailure)
            return Result<TOutput>.Failure(result.Error);

        var transformedValue = await transform(result.Value);
        return Result<TOutput>.Success(transformedValue);
    }

    /// <summary>
    /// Transforms the value if the async result is successful, allowing the transform to return a Result
    /// </summary>
    public static async Task<Result<TOutput>> BindAsync<T, TOutput>(this Task<Result<T>> resultTask, Func<T, Result<TOutput>> transform)
    {
        var result = await resultTask;
        return result.Bind(transform);
    }

    /// <summary>
    /// Transforms the value if the async result is successful, with async transform that returns Result
    /// </summary>
    public static async Task<Result<TOutput>> BindAsync<T, TOutput>(this Task<Result<T>> resultTask, Func<T, Task<Result<TOutput>>> transform)
    {
        var result = await resultTask;
        if (result.IsFailure)
            return Result<TOutput>.Failure(result.Error);

        return await transform(result.Value);
    }

    /// <summary>
    /// Executes an async action if the result is successful
    /// </summary>
    public static async Task<Result<T>> OnSuccessAsync<T>(this Task<Result<T>> resultTask, Func<T, Task> action)
    {
        var result = await resultTask;
        if (result.IsSuccess)
            await action(result.Value);
        return result;
    }

    /// <summary>
    /// Executes an async action if the result is failed
    /// </summary>
    public static async Task<Result<T>> OnFailureAsync<T>(this Task<Result<T>> resultTask, Func<Error, Task> action)
    {
        var result = await resultTask;
        if (result.IsFailure)
            await action(result.Error);
        return result;
    }

    /// <summary>
    /// Transforms the async result based on success or failure
    /// </summary>
    public static async Task<TOutput> MatchAsync<T, TOutput>(this Task<Result<T>> resultTask, Func<T, TOutput> onSuccess, Func<Error, TOutput> onFailure)
    {
        var result = await resultTask;
        return result.Match(onSuccess, onFailure);
    }

    /// <summary>
    /// Transforms the async result based on success or failure, with async transforms
    /// </summary>
    public static async Task<TOutput> MatchAsync<T, TOutput>(this Task<Result<T>> resultTask, Func<T, Task<TOutput>> onSuccess, Func<Error, Task<TOutput>> onFailure)
    {
        var result = await resultTask;
        if (result.IsSuccess)
            return await onSuccess(result.Value);
        
        return await onFailure(result.Error);
    }

    /// <summary>
    /// Provides a fallback async operation if the result is failed
    /// </summary>
    public static async Task<Result<T>> OrElseAsync<T>(this Task<Result<T>> resultTask, Func<Error, Task<Result<T>>> fallback)
    {
        var result = await resultTask;
        if (result.IsFailure)
            return await fallback(result.Error);
        
        return result;
    }

    #endregion

    #region Result Extensions for Sync to Async

    /// <summary>
    /// Transforms the value if the result is successful, with async transform
    /// </summary>
    public static async Task<Result<TOutput>> MapAsync<T, TOutput>(this Result<T> result, Func<T, Task<TOutput>> transform)
    {
        if (result.IsFailure)
            return Result<TOutput>.Failure(result.Error);

        var transformedValue = await transform(result.Value);
        return Result<TOutput>.Success(transformedValue);
    }

    /// <summary>
    /// Transforms the value if the result is successful, with async transform that returns Result
    /// </summary>
    public static async Task<Result<TOutput>> BindAsync<T, TOutput>(this Result<T> result, Func<T, Task<Result<TOutput>>> transform)
    {
        if (result.IsFailure)
            return Result<TOutput>.Failure(result.Error);

        return await transform(result.Value);
    }

    /// <summary>
    /// Executes an async action if the result is successful
    /// </summary>
    public static async Task<Result<T>> OnSuccessAsync<T>(this Result<T> result, Func<T, Task> action)
    {
        if (result.IsSuccess)
            await action(result.Value);
        return result;
    }

    /// <summary>
    /// Executes an async action if the result is failed
    /// </summary>
    public static async Task<Result<T>> OnFailureAsync<T>(this Result<T> result, Func<Error, Task> action)
    {
        if (result.IsFailure)
            await action(result.Error);
        return result;
    }

    #endregion

    #region Collection Extensions

    /// <summary>
    /// Combines multiple async results. Returns success only if all results are successful.
    /// </summary>
    public static async Task<Result<T[]>> CombineAsync<T>(params Task<Result<T>>[] resultTasks)
    {
        var results = await Task.WhenAll(resultTasks);
        return Result.Combine(results);
    }

    /// <summary>
    /// Combines multiple async results. Returns success only if all results are successful.
    /// </summary>
    public static async Task<Result<T[]>> CombineAsync<T>(IEnumerable<Task<Result<T>>> resultTasks)
    {
        var results = await Task.WhenAll(resultTasks);
        return Result.Combine(results.ToArray());
    }

    /// <summary>
    /// Maps a collection of values through an async transform that returns Results
    /// </summary>
    public static async Task<Result<TOutput[]>> TraverseAsync<T, TOutput>(this IEnumerable<T> source, Func<T, Task<Result<TOutput>>> transform)
    {
        var tasks = source.Select(transform);
        var results = await Task.WhenAll(tasks);
        return Result.Combine(results);
    }

    /// <summary>
    /// Maps a collection of values through a transform that returns Results
    /// </summary>
    public static Result<TOutput[]> Traverse<T, TOutput>(this IEnumerable<T> source, Func<T, Result<TOutput>> transform)
    {
        var results = source.Select(transform).ToArray();
        return Result.Combine(results);
    }

    #endregion

    #region Task<Result> (non-generic) Extensions

    /// <summary>
    /// Transforms an async non-generic Result to a generic Result
    /// </summary>
    public static async Task<Result<T>> MapAsync<T>(this Task<Result> resultTask, Func<T> valueFactory)
    {
        var result = await resultTask;
        if (result.IsFailure)
            return Result<T>.Failure(result.Error);

        return Result<T>.Success(valueFactory());
    }

    /// <summary>
    /// Transforms an async non-generic Result to a generic Result with async value factory
    /// </summary>
    public static async Task<Result<T>> MapAsync<T>(this Task<Result> resultTask, Func<Task<T>> valueFactory)
    {
        var result = await resultTask;
        if (result.IsFailure)
            return Result<T>.Failure(result.Error);

        var value = await valueFactory();
        return Result<T>.Success(value);
    }

    /// <summary>
    /// Chains an async operation to a non-generic Result
    /// </summary>
    public static async Task<Result<T>> BindAsync<T>(this Task<Result> resultTask, Func<Task<Result<T>>> transform)
    {
        var result = await resultTask;
        if (result.IsFailure)
            return Result<T>.Failure(result.Error);

        return await transform();
    }

    /// <summary>
    /// Executes an async action if the non-generic result is successful
    /// </summary>
    public static async Task<Result> OnSuccessAsync(this Task<Result> resultTask, Func<Task> action)
    {
        var result = await resultTask;
        if (result.IsSuccess)
            await action();
        return result;
    }

    /// <summary>
    /// Executes an async action if the non-generic result is failed
    /// </summary>
    public static async Task<Result> OnFailureAsync(this Task<Result> resultTask, Func<Error, Task> action)
    {
        var result = await resultTask;
        if (result.IsFailure)
            await action(result.Error);
        return result;
    }

    #endregion

    #region Utility Extensions

    /// <summary>
    /// Converts a Task&lt;T&gt; to a Task&lt;Result&lt;T&gt;&gt; by wrapping exceptions
    /// </summary>
    public static async Task<Result<T>> ToResultAsync<T>(this Task<T> task, Func<System.Exception, Error>? errorFactory = null)
    {
        try
        {
            var value = await task;
            return Result<T>.Success(value);
        }
        catch (System.Exception ex)
        {
            var error = errorFactory?.Invoke(ex) ?? Error.InternalError(ex.Message, innerException: ex);
            return Result<T>.Failure(error);
        }
    }

    /// <summary>
    /// Converts a Task to a Task&lt;Result&gt; by wrapping exceptions
    /// </summary>
    public static async Task<Result> ToResultAsync(this Task task, Func<System.Exception, Error>? errorFactory = null)
    {
        try
        {
            await task;
            return Result.Success();
        }
        catch (System.Exception ex)
        {
            var error = errorFactory?.Invoke(ex) ?? Error.InternalError(ex.Message, innerException: ex);
            return Result.Failure(error);
        }
    }

    /// <summary>
    /// Safely gets the value or returns default if failed
    /// </summary>
    public static async Task<T?> GetValueOrDefaultAsync<T>(this Task<Result<T>> resultTask, T? defaultValue = default)
    {
        var result = await resultTask;
        return result.IsSuccess ? result.Value : defaultValue;
    }

    /// <summary>
    /// Safely gets the value or computes default if failed
    /// </summary>
    public static async Task<T> GetValueOrDefaultAsync<T>(this Task<Result<T>> resultTask, Func<Error, T> defaultValueFactory)
    {
        var result = await resultTask;
        return result.IsSuccess ? result.Value : defaultValueFactory(result.Error);
    }

    /// <summary>
    /// Safely gets the value or computes default asynchronously if failed
    /// </summary>
    public static async Task<T> GetValueOrDefaultAsync<T>(this Task<Result<T>> resultTask, Func<Error, Task<T>> defaultValueFactory)
    {
        var result = await resultTask;
        if (result.IsSuccess)
            return result.Value;
        
        return await defaultValueFactory(result.Error);
    }

    #endregion
}