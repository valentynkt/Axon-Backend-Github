using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Functional.Async;

/// <summary>
/// Advanced async patterns and extensions for Result types.
/// Provides Task{Result{T}} extensions and AsyncEnumerable support.
/// Enables sophisticated async result composition and streaming scenarios.
/// </summary>
public static class AsyncResultExtensions
{
    #region Task<Result<T>> Extensions

    /// <summary>
    /// Maps a Task{Result{T}} to Task{Result{TNew}} using the provided mapper function.
    /// Handles async mapping while maintaining Result pattern semantics.
    /// </summary>
    /// <typeparam name="T">Source type</typeparam>
    /// <typeparam name="TNew">Target type</typeparam>
    /// <param name="taskResult">The task result to map</param>
    /// <param name="mapper">The mapping function</param>
    /// <returns>A new Task{Result{TNew}} with the mapped value</returns>
    public static async Task<Result<TNew>> MapAsync<T, TNew>(
        this Task<Result<T>> taskResult,
        Func<T, TNew> mapper)
    {
        ArgumentNullException.ThrowIfNull(taskResult);
        ArgumentNullException.ThrowIfNull(mapper);

        var result = await taskResult.ConfigureAwait(false);
        return result.Map(mapper);
    }

    /// <summary>
    /// Maps a Task{Result{T}} to Task{Result{TNew}} using an async mapper function.
    /// </summary>
    /// <typeparam name="T">Source type</typeparam>
    /// <typeparam name="TNew">Target type</typeparam>
    /// <param name="taskResult">The task result to map</param>
    /// <param name="asyncMapper">The async mapping function</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A new Task{Result{TNew}} with the mapped value</returns>
    public static async Task<Result<TNew>> MapAsync<T, TNew>(
        this Task<Result<T>> taskResult,
        Func<T, Task<TNew>> asyncMapper,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taskResult);
        ArgumentNullException.ThrowIfNull(asyncMapper);

        var result = await taskResult.ConfigureAwait(false);
        if (result.IsFailure)
            return Result<TNew>.Failure(result.Error);

        try
        {
            var mappedValue = await asyncMapper(result.Value).ConfigureAwait(false);
            return Result<TNew>.Success(mappedValue);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<TNew>.Failure(Error.Cancelled("Mapping operation was cancelled"));
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure(Error.FromException(ex));
        }
    }

    /// <summary>
    /// Binds a Task{Result{T}} to a new Task{Result{TNew}} using the provided binder function.
    /// Enables chaining of async Result operations.
    /// </summary>
    /// <typeparam name="T">Source type</typeparam>
    /// <typeparam name="TNew">Target type</typeparam>
    /// <param name="taskResult">The task result to bind</param>
    /// <param name="binder">The binding function</param>
    /// <returns>The bound task result</returns>
    public static async Task<Result<TNew>> BindAsync<T, TNew>(
        this Task<Result<T>> taskResult,
        Func<T, Result<TNew>> binder)
    {
        ArgumentNullException.ThrowIfNull(taskResult);
        ArgumentNullException.ThrowIfNull(binder);

        var result = await taskResult.ConfigureAwait(false);
        return result.Bind(binder);
    }

    /// <summary>
    /// Binds a Task{Result{T}} to a new Task{Result{TNew}} using an async binder function.
    /// </summary>
    /// <typeparam name="T">Source type</typeparam>
    /// <typeparam name="TNew">Target type</typeparam>
    /// <param name="taskResult">The task result to bind</param>
    /// <param name="asyncBinder">The async binding function</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The bound task result</returns>
    public static async Task<Result<TNew>> BindAsync<T, TNew>(
        this Task<Result<T>> taskResult,
        Func<T, Task<Result<TNew>>> asyncBinder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taskResult);
        ArgumentNullException.ThrowIfNull(asyncBinder);

        var result = await taskResult.ConfigureAwait(false);
        if (result.IsFailure)
            return Result<TNew>.Failure(result.Error);

        return await asyncBinder(result.Value).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an action on successful Task{Result{T}} without changing the result.
    /// Useful for side effects like logging or notifications.
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="taskResult">The task result to tap</param>
    /// <param name="action">The action to execute on success</param>
    /// <returns>The original task result</returns>
    public static async Task<Result<T>> TapAsync<T>(
        this Task<Result<T>> taskResult,
        Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(taskResult);
        ArgumentNullException.ThrowIfNull(action);

        var result = await taskResult.ConfigureAwait(false);
        return result.Tap(action);
    }

    /// <summary>
    /// Executes an async action on successful Task{Result{T}} without changing the result.
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="taskResult">The task result to tap</param>
    /// <param name="asyncAction">The async action to execute on success</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The original task result</returns>
    public static async Task<Result<T>> TapAsync<T>(
        this Task<Result<T>> taskResult,
        Func<T, Task> asyncAction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taskResult);
        ArgumentNullException.ThrowIfNull(asyncAction);

        var result = await taskResult.ConfigureAwait(false);
        
        if (result.IsSuccess)
        {
            try
            {
                await asyncAction(result.Value).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Don't change the result on cancellation of side effect
            }
            catch
            {
                // Don't change the result on side effect failure
            }
        }

        return result;
    }

    /// <summary>
    /// Executes an action on failed Task{Result{T}} without changing the result.
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="taskResult">The task result to tap</param>
    /// <param name="action">The action to execute on failure</param>
    /// <returns>The original task result</returns>
    public static async Task<Result<T>> TapErrorAsync<T>(
        this Task<Result<T>> taskResult,
        Action<Error> action)
    {
        ArgumentNullException.ThrowIfNull(taskResult);
        ArgumentNullException.ThrowIfNull(action);

        var result = await taskResult.ConfigureAwait(false);
        return result.TapError(action);
    }

    /// <summary>
    /// Provides a fallback value if the Task{Result{T}} represents a failure.
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="taskResult">The task result</param>
    /// <param name="fallbackValue">The fallback value to use on failure</param>
    /// <returns>The success value or the fallback value</returns>
    public static async Task<T> GetValueOrElseAsync<T>(
        this Task<Result<T>> taskResult,
        T fallbackValue)
    {
        ArgumentNullException.ThrowIfNull(taskResult);

        var result = await taskResult.ConfigureAwait(false);
        return result.GetOrElse(fallbackValue);
    }

    /// <summary>
    /// Provides a fallback value from a factory if the Task{Result{T}} represents a failure.
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="taskResult">The task result</param>
    /// <param name="fallbackFactory">The factory function to create fallback value</param>
    /// <returns>The success value or the fallback value</returns>
    public static async Task<T> GetValueOrElseAsync<T>(
        this Task<Result<T>> taskResult,
        Func<Error, T> fallbackFactory)
    {
        ArgumentNullException.ThrowIfNull(taskResult);
        ArgumentNullException.ThrowIfNull(fallbackFactory);

        var result = await taskResult.ConfigureAwait(false);
        return result.GetOrElse(fallbackFactory);
    }

    #endregion

    #region AsyncEnumerable Support

    /// <summary>
    /// Transforms an AsyncEnumerable of items into an AsyncEnumerable of Results.
    /// Each item is wrapped in a successful Result.
    /// </summary>
    /// <typeparam name="T">The item type</typeparam>
    /// <param name="source">The source async enumerable</param>
    /// <returns>An AsyncEnumerable of successful Results</returns>
    public static async IAsyncEnumerable<Result<T>> ToResultsAsync<T>(
        this IAsyncEnumerable<T> source,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            yield return Result<T>.Success(item);
        }
    }

    /// <summary>
    /// Safely transforms an AsyncEnumerable where each transformation can fail.
    /// Returns only successful transformations, logging failures.
    /// </summary>
    /// <typeparam name="TIn">Input type</typeparam>
    /// <typeparam name="TOut">Output type</typeparam>
    /// <param name="source">The source async enumerable</param>
    /// <param name="transform">The transformation function that returns Result</param>
    /// <param name="onError">Optional error handler</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An AsyncEnumerable of successful transformations</returns>
    public static async IAsyncEnumerable<TOut> SelectManyResultsAsync<TIn, TOut>(
        this IAsyncEnumerable<TIn> source,
        Func<TIn, Result<TOut>> transform,
        Action<Error>? onError = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(transform);

        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            var result = transform(item);
            if (result.IsSuccess)
            {
                yield return result.Value;
            }
            else
            {
                onError?.Invoke(result.Error);
            }
        }
    }

    /// <summary>
    /// Transforms an AsyncEnumerable using an async transformation that returns Results.
    /// Returns only successful transformations.
    /// </summary>
    /// <typeparam name="TIn">Input type</typeparam>
    /// <typeparam name="TOut">Output type</typeparam>
    /// <param name="source">The source async enumerable</param>
    /// <param name="asyncTransform">The async transformation function</param>
    /// <param name="onError">Optional error handler</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An AsyncEnumerable of successful transformations</returns>
    public static async IAsyncEnumerable<TOut> SelectManyResultsAsync<TIn, TOut>(
        this IAsyncEnumerable<TIn> source,
        Func<TIn, Task<Result<TOut>>> asyncTransform,
        Action<Error>? onError = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(asyncTransform);

        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            var result = await asyncTransform(item);
            if (result.IsSuccess)
            {
                yield return result.Value;
            }
            else
            {
                onError?.Invoke(result.Error);
            }
        }
    }

    /// <summary>
    /// Filters an AsyncEnumerable of Results to only successful ones.
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="source">The source async enumerable of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An AsyncEnumerable of successful values</returns>
    public static async IAsyncEnumerable<T> WhereSuccessAsync<T>(
        this IAsyncEnumerable<Result<T>> source,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        await foreach (var result in source.WithCancellation(cancellationToken))
        {
            if (result.IsSuccess)
                yield return result.Value;
        }
    }

    /// <summary>
    /// Filters an AsyncEnumerable of Results to only failed ones.
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="source">The source async enumerable of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An AsyncEnumerable of errors</returns>
    public static async IAsyncEnumerable<Error> WhereFailureAsync<T>(
        this IAsyncEnumerable<Result<T>> source,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        await foreach (var result in source.WithCancellation(cancellationToken))
        {
            if (result.IsFailure)
                yield return result.Error;
        }
    }

    /// <summary>
    /// Collects all successful values from an AsyncEnumerable of Results into a List.
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="source">The source async enumerable of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A Result containing the list of successful values</returns>
    public static async Task<Result<IReadOnlyList<T>>> ToListAsync<T>(
        this IAsyncEnumerable<Result<T>> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            var results = new List<T>();
            var errors = new List<Error>();

            await foreach (var result in source.WithCancellation(cancellationToken))
            {
                if (result.IsSuccess)
                    results.Add(result.Value);
                else
                    errors.Add(result.Error);
            }

            if (errors.Count != 0)
            {
                var aggregatedError = errors.Count == 1 
                    ? errors[0] 
                    : Error.Aggregate(errors.ToArray());
                return Result<IReadOnlyList<T>>.Failure(aggregatedError);
            }

            return Result<IReadOnlyList<T>>.Success(results.AsReadOnly());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<IReadOnlyList<T>>.Failure(Error.Cancelled("Operation was cancelled"));
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<T>>.Failure(Error.FromException(ex));
        }
    }

    /// <summary>
    /// Batches an AsyncEnumerable of Results into chunks for parallel processing.
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="source">The source async enumerable</param>
    /// <param name="batchSize">The size of each batch</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An AsyncEnumerable of batches</returns>
    public static async IAsyncEnumerable<IReadOnlyList<T>> BatchAsync<T>(
        this IAsyncEnumerable<T> source,
        int batchSize,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        
        if (batchSize <= 0)
            throw new ArgumentException("Batch size must be greater than 0", nameof(batchSize));

        var batch = new List<T>(batchSize);

        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            batch.Add(item);

            if (batch.Count == batchSize)
            {
                yield return batch.AsReadOnly();
                batch.Clear();
            }
        }

        // Yield any remaining items
        if (batch.Count > 0)
        {
            yield return batch.AsReadOnly();
        }
    }

    #endregion
}