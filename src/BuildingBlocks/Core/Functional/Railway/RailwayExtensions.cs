using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Functional.Railway;

/// <summary>
/// Advanced railway programming extensions for Result pattern.
/// Provides sequence operations, parallel execution, and conditional patterns.
/// Enables complex result composition while maintaining railway-oriented programming principles.
/// W3C mode: CorrelationId is always the current Activity TraceId.
/// </summary>
public static class RailwayExtensions
{
    #region Sequence Operations

    /// <summary>
    /// Combines a sequence of Results into a single Result containing all success values.
    /// If any Result is a failure, returns the first failure encountered.
    /// </summary>
    /// <typeparam name="T">The type of values in the results</typeparam>
    /// <param name="results">The sequence of results to combine</param>
    /// <returns>A Result containing all success values or the first failure</returns>
    public static Result<IReadOnlyList<T>> Sequence<T>(this IEnumerable<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var resultsList = results.ToList();
        if (resultsList.Count == 0)
            return Result<IReadOnlyList<T>>.Success(Array.Empty<T>());

        var values = new List<T>();

        foreach (var result in resultsList)
        {
            if (result.IsFailure)
                return Result<IReadOnlyList<T>>.Failure(WithCorrelation(result.Error));

            values.Add(result.Value);
        }

        return Result<IReadOnlyList<T>>.Success(values.AsReadOnly());
    }

    /// <summary>
    /// Combines a sequence of Results, collecting all failures if any exist.
    /// If all succeed, returns success with all values.
    /// If any fail, returns failure with aggregated errors.
    /// </summary>
    /// <typeparam name="T">The type of values in the results</typeparam>
    /// <param name="results">The sequence of results to combine</param>
    /// <returns>A Result containing all success values or aggregated failures</returns>
    public static Result<IReadOnlyList<T>> SequenceWithAllErrors<T>(this IEnumerable<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var resultsList = results.ToList();
        if (resultsList.Count == 0)
            return Result<IReadOnlyList<T>>.Success(Array.Empty<T>());

        var values = new List<T>();
        var errors = new List<Error>();

        foreach (var result in resultsList)
        {
            if (result.IsSuccess)
                values.Add(result.Value);
            else
                errors.Add(result.Error);
        }

        if (errors.Count != 0)
        {
            var aggregatedError = errors.Count == 1
                ? errors[0]
                : Error.Aggregate(errors.ToArray());

            return Result<IReadOnlyList<T>>.Failure(WithCorrelation(aggregatedError));
        }

        return Result<IReadOnlyList<T>>.Success(values.AsReadOnly());
    }

    /// <summary>
    /// Applies a function to each item in a sequence, returning all successful results.
    /// Failed operations are filtered out, allowing partial success scenarios.
    /// </summary>
    public static Result<IReadOnlyList<TOut>> TraversePartial<TIn, TOut>(
        this IEnumerable<TIn> items,
        Func<TIn, Result<TOut>> transform)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(transform);

        var results = new List<TOut>();

        foreach (var item in items)
        {
            var result = transform(item);
            if (result.IsSuccess)
                results.Add(result.Value);
        }

        return Result<IReadOnlyList<TOut>>.Success(results.AsReadOnly());
    }

    /// <summary>
    /// Applies a function to each item in a sequence, requiring all to succeed.
    /// Returns the first failure encountered or all successful results.
    /// </summary>
    public static Result<IReadOnlyList<TOut>> Traverse<TIn, TOut>(
        this IEnumerable<TIn> items,
        Func<TIn, Result<TOut>> transform)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(transform);

        return items.Select(transform).Sequence();
    }

    #endregion

    #region Parallel Execution

    /// <summary>
    /// Executes multiple Result-returning operations in parallel and combines their results.
    /// Returns the first failure encountered or all successful results.
    /// </summary>
    public static async Task<Result<IReadOnlyList<T>>> ParallelSequence<T>(
        this IEnumerable<Func<CancellationToken, Task<Result<T>>>> operations,
        int maxDegreeOfParallelism = -1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var operationsList = operations.ToList();
        if (operationsList.Count == 0)
            return Result<IReadOnlyList<T>>.Success(Array.Empty<T>());

        var actualMaxDegree = maxDegreeOfParallelism == -1 ? Environment.ProcessorCount : maxDegreeOfParallelism;

        try
        {
            using var semaphore = new SemaphoreSlim(actualMaxDegree, actualMaxDegree);
            var tasks = operationsList.Select(async operation =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await operation(cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);

            // Ensure all failures carry the current correlation id
            var normalized = results.Select(EnsureResultCorrelation).ToArray();

            return normalized.Sequence();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<IReadOnlyList<T>>.Failure(WithCorrelation(Error.Cancelled("Parallel operation was cancelled")));
        }
    }

    /// <summary>
    /// Executes multiple Result-returning operations in parallel, collecting all errors.
    /// Returns success with all values if all succeed, or failure with aggregated errors.
    /// </summary>
    public static async Task<Result<IReadOnlyList<T>>> ParallelSequenceWithAllErrors<T>(
        this IEnumerable<Func<CancellationToken, Task<Result<T>>>> operations,
        int maxDegreeOfParallelism = -1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var operationsList = operations.ToList();
        if (operationsList.Count == 0)
            return Result<IReadOnlyList<T>>.Success(Array.Empty<T>());

        var actualMaxDegree = maxDegreeOfParallelism == -1 ? Environment.ProcessorCount : maxDegreeOfParallelism;

        try
        {
            using var semaphore = new SemaphoreSlim(actualMaxDegree, actualMaxDegree);
            var tasks = operationsList.Select(async operation =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await operation(cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);
            var normalized = results.Select(EnsureResultCorrelation).ToArray();

            return normalized.SequenceWithAllErrors();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<IReadOnlyList<T>>.Failure(WithCorrelation(Error.Cancelled("Parallel operation was cancelled")));
        }
    }

    /// <summary>
    /// Executes operations in parallel but allows partial success.
    /// Returns all successful results, ignoring failures.
    /// </summary>
    public static async Task<Result<IReadOnlyList<T>>> ParallelPartial<T>(
        this IEnumerable<Func<CancellationToken, Task<Result<T>>>> operations,
        int maxDegreeOfParallelism = -1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var operationsList = operations.ToList();
        if (operationsList.Count == 0)
            return Result<IReadOnlyList<T>>.Success(Array.Empty<T>());

        var actualMaxDegree = maxDegreeOfParallelism == -1 ? Environment.ProcessorCount : maxDegreeOfParallelism;

        try
        {
            var results = new ConcurrentBag<T>();
            using var semaphore = new SemaphoreSlim(actualMaxDegree, actualMaxDegree);

            var tasks = operationsList.Select(async operation =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var result = EnsureResultCorrelation(await operation(cancellationToken));
                    if (result.IsSuccess)
                        results.Add(result.Value);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return Result<IReadOnlyList<T>>.Success(results.ToList().AsReadOnly());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<IReadOnlyList<T>>.Failure(WithCorrelation(Error.Cancelled("Parallel operation was cancelled")));
        }
    }

    #endregion

    #region Conditional Railway Patterns

    /// <summary>
    /// Executes one of two functions based on a condition, both returning Results.
    /// </summary>
    public static Result<T> Branch<T>(
        bool condition,
        Func<Result<T>> trueFunc,
        Func<Result<T>> falseFunc)
    {
        ArgumentNullException.ThrowIfNull(trueFunc);
        ArgumentNullException.ThrowIfNull(falseFunc);

        return condition ? trueFunc() : falseFunc();
    }

    /// <summary>
    /// Executes one of two async functions based on a condition, both returning Results.
    /// </summary>
    public static async Task<Result<T>> BranchAsync<T>(
        bool condition,
        Func<CancellationToken, Task<Result<T>>> trueFunc,
        Func<CancellationToken, Task<Result<T>>> falseFunc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trueFunc);
        ArgumentNullException.ThrowIfNull(falseFunc);

        return condition
            ? await trueFunc(cancellationToken)
            : await falseFunc(cancellationToken);
    }

    /// <summary>
    /// Conditional bind - legacy overload (surprising for differing T/TNew). Prefer BindIfSame or the overload with a default factory.
    /// </summary>
    [Obsolete("This overload is surprising because it returns Result<TNew> and cannot 'return the original result'. " +
              "Use BindIfSame for same-type results or the BindIf overload with defaultValueFactory.")]
    public static Result<TNew> BindIf<T, TNew>(
        this Result<T> result,
        bool condition,
        Func<T, Result<TNew>> binder)
        where TNew : new()
    {
        ArgumentNullException.ThrowIfNull(binder);

        if (result.IsFailure)
            return Result<TNew>.Failure(WithCorrelation(result.Error));

        return condition
            ? binder(result.Value)
            : Result<TNew>.Success(new TNew());
    }

    /// <summary>
    /// Conditional bind with predicate function - executes the binder only if predicate returns true.
    /// </summary>
    public static Result<TNew> BindIf<T, TNew>(
        this Result<T> result,
        Func<T, bool> predicate,
        Func<T, Result<TNew>> binder,
        Func<T, TNew> defaultValueFactory)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(binder);
        ArgumentNullException.ThrowIfNull(defaultValueFactory);

        if (result.IsFailure)
            return Result<TNew>.Failure(WithCorrelation(result.Error));

        return predicate(result.Value)
            ? binder(result.Value)
            : Result<TNew>.Success(defaultValueFactory(result.Value));
    }

    /// <summary>
    /// Conditional bind that preserves the same type: if condition is false, returns the original result.
    /// </summary>
    public static Result<T> BindIfSame<T>(
        this Result<T> result,
        bool condition,
        Func<T, Result<T>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        if (result.IsFailure) return result;
        return condition ? binder(result.Value) : result;
    }

    /// <summary>
    /// Pipeline multiple operations in sequence, short-circuiting on first failure.
    /// Each operation receives the result of the previous operation.
    /// </summary>
    public static Result<T> Pipeline<T>(
        this Result<T> initialResult,
        params Func<T, Result<T>>[] operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var current = initialResult;

        foreach (var operation in operations)
        {
            if (current.IsFailure)
                return EnsureResultCorrelation(current);

            current = operation(current.Value);
        }

        return current;
    }

    /// <summary>
    /// Async pipeline for sequential operations with short-circuiting.
    /// </summary>
    public static async Task<Result<T>> PipelineAsync<T>(
        this Result<T> initialResult,
        IEnumerable<Func<T, CancellationToken, Task<Result<T>>>> operations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var current = initialResult;

        foreach (var operation in operations)
        {
            if (current.IsFailure)
                return EnsureResultCorrelation(current);

            current = await operation(current.Value, cancellationToken);
        }

        return current;
    }

    #endregion

    #region Retry Patterns

    /// <summary>
    /// Retries an operation that returns a Result with exponential backoff.
    /// Only retries on specific error types that might be transient.
    /// </summary>
    public static async Task<Result<T>> RetryAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        int maxAttempts = 3,
        TimeSpan? baseDelay = null,
        ErrorType[]? retryableErrorTypes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (maxAttempts <= 0)
            throw new ArgumentException("Max attempts must be greater than 0", nameof(maxAttempts));

        var delay = baseDelay ?? TimeSpan.FromMilliseconds(100);

        // Sensible transient defaults aligned with Axon ErrorType
        var retryableTypes = retryableErrorTypes ?? new[]
        {
            ErrorType.Timeout,
            ErrorType.Unavailable,
            ErrorType.Network,
            ErrorType.External,
            ErrorType.Concurrency
        };

        Result<T> lastResult = Result<T>.Failure(Error.Internal("Retry not attempted"));

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            lastResult = EnsureResultCorrelation(await operation(cancellationToken));

            if (lastResult.IsSuccess)
                return lastResult;

            if (ShouldStopRetrying(lastResult.Error, retryableTypes, attempt, maxAttempts))
                break;

            await DelayWithTelemetry(delay, attempt, cancellationToken);
        }

        return EnsureResultCorrelation(lastResult);
    }

    private static bool ShouldStopRetrying(Error error, ErrorType[] retryableTypes, int attempt, int maxAttempts)
        => !retryableTypes.Contains(error.Type) || attempt >= maxAttempts;

    private static async Task DelayWithTelemetry(TimeSpan baseDelay, int attempt, CancellationToken cancellationToken)
    {
        var actualDelay = TimeSpan.FromTicks(baseDelay.Ticks * (long)Math.Pow(2, attempt - 1));

        Activity.Current?.AddEvent(new ActivityEvent(
            "retry.attempt",
            tags: new ActivityTagsCollection
            {
                ["attempt"] = attempt,
                ["next_delay_ms"] = actualDelay.TotalMilliseconds,
                ["correlation.id"] = Activity.Current?.TraceId.ToString()
            }));

        try
        {
            await Task.Delay(actualDelay, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Operation was cancelled during retry", cancellationToken);
        }
    }

    #endregion

    #region Helpers (W3C Correlation)

    private static Result<T> EnsureResultCorrelation<T>(Result<T> result)
        => result.IsFailure ? Result<T>.Failure(WithCorrelation(result.Error)) : result;

    private static Error WithCorrelation(Error error)
    {
        if (!string.IsNullOrWhiteSpace(error.CorrelationId))
            return error;

        var traceId = Activity.Current?.TraceId.ToString();
        return error.WithCorrelationId(traceId ?? ActivityTraceId.CreateRandom().ToString());
    }

    #endregion
}
