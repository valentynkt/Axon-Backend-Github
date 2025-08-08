using BuildingBlocks.Core.Functional.Results;
using System.Collections.Concurrent;

namespace BuildingBlocks.Core.Functional.Railway;

/// <summary>
/// Advanced railway programming extensions for Result pattern.
/// Provides sequence operations, parallel execution, and conditional patterns.
/// Enables complex result composition while maintaining railway-oriented programming principles.
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
                return Result<IReadOnlyList<T>>.Failure(result.Error);
                
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
            return Result<IReadOnlyList<T>>.Failure(aggregatedError);
        }

        return Result<IReadOnlyList<T>>.Success(values.AsReadOnly());
    }

    /// <summary>
    /// Applies a function to each item in a sequence, returning all successful results.
    /// Failed operations are filtered out, allowing partial success scenarios.
    /// </summary>
    /// <typeparam name="TIn">Input type</typeparam>
    /// <typeparam name="TOut">Output type</typeparam>
    /// <param name="items">The items to transform</param>
    /// <param name="transform">The transformation function that returns Result</param>
    /// <returns>A Result containing all successful transformations</returns>
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
    /// <typeparam name="TIn">Input type</typeparam>
    /// <typeparam name="TOut">Output type</typeparam>
    /// <param name="items">The items to transform</param>
    /// <param name="transform">The transformation function that returns Result</param>
    /// <returns>A Result containing all successful transformations or the first failure</returns>
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
    /// <typeparam name="T">The type of values in the results</typeparam>
    /// <param name="operations">The operations to execute in parallel</param>
    /// <param name="maxDegreeOfParallelism">Maximum number of concurrent operations</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A Result containing all success values or the first failure</returns>
    public static async Task<Result<IReadOnlyList<T>>> ParallelSequence<T>(
        this IEnumerable<Func<CancellationToken, Task<Result<T>>>> operations,
        int maxDegreeOfParallelism = -1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var operationsList = operations.ToList();
        if (operationsList.Count == 0)
            return Result<IReadOnlyList<T>>.Success(Array.Empty<T>());

        // Use processor count if -1 is specified
        var actualMaxDegree = maxDegreeOfParallelism == -1 ? Environment.ProcessorCount : maxDegreeOfParallelism;

        try
        {
            var semaphore = new SemaphoreSlim(actualMaxDegree, actualMaxDegree);
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
            return results.Sequence();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<IReadOnlyList<T>>.Failure(Error.Cancelled("Parallel operation was cancelled"));
        }
    }

    /// <summary>
    /// Executes multiple Result-returning operations in parallel, collecting all errors.
    /// Returns success with all values if all succeed, or failure with aggregated errors.
    /// </summary>
    /// <typeparam name="T">The type of values in the results</typeparam>
    /// <param name="operations">The operations to execute in parallel</param>
    /// <param name="maxDegreeOfParallelism">Maximum number of concurrent operations</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A Result containing all success values or aggregated failures</returns>
    public static async Task<Result<IReadOnlyList<T>>> ParallelSequenceWithAllErrors<T>(
        this IEnumerable<Func<CancellationToken, Task<Result<T>>>> operations,
        int maxDegreeOfParallelism = -1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var operationsList = operations.ToList();
        if (operationsList.Count == 0)
            return Result<IReadOnlyList<T>>.Success(Array.Empty<T>());

        // Use processor count if -1 is specified
        var actualMaxDegree = maxDegreeOfParallelism == -1 ? Environment.ProcessorCount : maxDegreeOfParallelism;

        try
        {
            var semaphore = new SemaphoreSlim(actualMaxDegree, actualMaxDegree);
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
            return results.SequenceWithAllErrors();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<IReadOnlyList<T>>.Failure(Error.Cancelled("Parallel operation was cancelled"));
        }
    }

    /// <summary>
    /// Executes operations in parallel but allows partial success.
    /// Returns all successful results, ignoring failures.
    /// </summary>
    /// <typeparam name="T">The type of values in the results</typeparam>
    /// <param name="operations">The operations to execute in parallel</param>
    /// <param name="maxDegreeOfParallelism">Maximum number of concurrent operations</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A Result containing all successful values</returns>
    public static async Task<Result<IReadOnlyList<T>>> ParallelPartial<T>(
        this IEnumerable<Func<CancellationToken, Task<Result<T>>>> operations,
        int maxDegreeOfParallelism = -1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var operationsList = operations.ToList();
        if (operationsList.Count == 0)
            return Result<IReadOnlyList<T>>.Success(Array.Empty<T>());

        // Use processor count if -1 is specified
        var actualMaxDegree = maxDegreeOfParallelism == -1 ? Environment.ProcessorCount : maxDegreeOfParallelism;

        try
        {
            var results = new ConcurrentBag<T>();
            var semaphore = new SemaphoreSlim(actualMaxDegree, actualMaxDegree);
            
            var tasks = operationsList.Select(async operation =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var result = await operation(cancellationToken);
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
            return Result<IReadOnlyList<T>>.Failure(Error.Cancelled("Parallel operation was cancelled"));
        }
    }

    #endregion

    #region Conditional Railway Patterns

    /// <summary>
    /// Executes one of two functions based on a condition, both returning Results.
    /// Maintains railway programming by handling both success and failure paths.
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="condition">The condition to evaluate</param>
    /// <param name="trueFunc">Function to execute if condition is true</param>
    /// <param name="falseFunc">Function to execute if condition is false</param>
    /// <returns>The result of the executed function</returns>
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
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="condition">The condition to evaluate</param>
    /// <param name="trueFunc">Async function to execute if condition is true</param>
    /// <param name="falseFunc">Async function to execute if condition is false</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the executed function</returns>
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
    /// Conditional bind - executes the binder only if the condition is met.
    /// If condition is false, returns the original result unchanged.
    /// </summary>
    /// <typeparam name="T">The current result type</typeparam>
    /// <typeparam name="TNew">The new result type</typeparam>
    /// <param name="result">The result to potentially bind</param>
    /// <param name="condition">The condition to check</param>
    /// <param name="binder">The binding function to execute if condition is true</param>
    /// <returns>The bound result if condition is true, otherwise a successful result with default value</returns>
    public static Result<TNew> BindIf<T, TNew>(
        this Result<T> result,
        bool condition,
        Func<T, Result<TNew>> binder)
        where TNew : new()
    {
        ArgumentNullException.ThrowIfNull(binder);

        if (result.IsFailure)
            return Result<TNew>.Failure(result.Error);

        return condition 
            ? binder(result.Value) 
            : Result<TNew>.Success(new TNew());
    }

    /// <summary>
    /// Conditional bind with predicate function - executes the binder only if predicate returns true.
    /// </summary>
    /// <typeparam name="T">The current result type</typeparam>
    /// <typeparam name="TNew">The new result type</typeparam>
    /// <param name="result">The result to potentially bind</param>
    /// <param name="predicate">The predicate function to evaluate</param>
    /// <param name="binder">The binding function to execute if predicate is true</param>
    /// <param name="defaultValueFactory">Factory for default value when predicate is false</param>
    /// <returns>The bound result if predicate is true, otherwise default value</returns>
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
            return Result<TNew>.Failure(result.Error);

        return predicate(result.Value)
            ? binder(result.Value)
            : Result<TNew>.Success(defaultValueFactory(result.Value));
    }

    /// <summary>
    /// Pipeline multiple operations in sequence, short-circuiting on first failure.
    /// Each operation receives the result of the previous operation.
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="initialResult">The initial result to start the pipeline</param>
    /// <param name="operations">The operations to execute in sequence</param>
    /// <returns>The final result after all operations</returns>
    public static Result<T> Pipeline<T>(
        this Result<T> initialResult,
        params Func<T, Result<T>>[] operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var current = initialResult;
        
        foreach (var operation in operations)
        {
            if (current.IsFailure)
                return current;
                
            current = operation(current.Value);
        }

        return current;
    }

    /// <summary>
    /// Async pipeline for sequential operations with short-circuiting.
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="initialResult">The initial result to start the pipeline</param>
    /// <param name="operations">The async operations to execute in sequence</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The final result after all operations</returns>
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
                return current;
                
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
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="operation">The operation to retry</param>
    /// <param name="maxAttempts">Maximum number of attempts</param>
    /// <param name="baseDelay">Base delay between attempts</param>
    /// <param name="retryableErrorTypes">Error types that should trigger a retry</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the operation or the final failure</returns>
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
        var defaultRetryableTypes = new[] { ErrorType.System, ErrorType.Cancellation };
        var retryableTypes = retryableErrorTypes ?? defaultRetryableTypes;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var result = await operation(cancellationToken);
            
            if (result.IsSuccess)
                return result;

            // Don't retry if error type is not retryable
            if (!retryableTypes.Contains(result.Error.Type))
                return result;

            // Don't retry on last attempt
            if (attempt == maxAttempts)
                return result;

            // Calculate exponential backoff delay
            var actualDelay = TimeSpan.FromTicks(delay.Ticks * (long)Math.Pow(2, attempt - 1));
            
            try
            {
                await Task.Delay(actualDelay, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Result<T>.Failure(Error.Cancelled("Operation was cancelled during retry"));
            }
        }

        // This should never be reached due to the logic above, but included for completeness
        return Result<T>.Failure(Error.System("Retry logic error - should not reach this point"));
    }

    #endregion
}