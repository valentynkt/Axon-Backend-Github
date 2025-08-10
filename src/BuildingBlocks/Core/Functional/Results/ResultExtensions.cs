#pragma warning disable CA1031
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Functional.Results;

/// <summary>
/// Consolidated sync/async helpers for Result/Result&lt;T&gt; (+ streaming & collections).
/// This file replaces Async/AsyncResultExtensions.cs to remove duplication.
/// </summary>
public static class ResultExtensions
{
    #region Task<Result<T>> helpers

    public static async Task<Result<TOut>> MapAsync<T, TOut>(this Task<Result<T>> task, Func<T, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(map);
        var r = await task.ConfigureAwait(false);
        return r.Map(map);
    }

    public static async Task<Result<TOut>> MapAsync<T, TOut>(this Task<Result<T>> task, Func<T, Task<TOut>> map)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(map);
        var r = await task.ConfigureAwait(false);
        return r.IsFailure
            ? Results.Failure<TOut>(r.Error)
            : await r.MapAsync(map).ConfigureAwait(false);
    }

    public static async Task<Result<TOut>> BindAsync<T, TOut>(this Task<Result<T>> task, Func<T, Result<TOut>> bind)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(bind);
        var r = await task.ConfigureAwait(false);
        return r.Bind(bind);
    }

    public static async Task<Result<TOut>> BindAsync<T, TOut>(this Task<Result<T>> task, Func<T, Task<Result<TOut>>> bind)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(bind);
        var r = await task.ConfigureAwait(false);
        return r.IsFailure ? Results.Failure<TOut>(r.Error) : await bind(r.Value).ConfigureAwait(false);
    }

    public static async Task<Result<T>> OnSuccessAsync<T>(this Task<Result<T>> task, Func<T, Task> action)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(action);
        var r = await task.ConfigureAwait(false);
        if (r.IsSuccess) await action(r.Value).ConfigureAwait(false);
        return r;
    }

    public static async Task<Result<T>> OnFailureAsync<T>(this Task<Result<T>> task, Func<Error, Task> action)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(action);
        var r = await task.ConfigureAwait(false);
        if (r.IsFailure) await action(r.Error).ConfigureAwait(false);
        return r;
    }

    public static async Task<TOut> MatchAsync<T, TOut>(this Task<Result<T>> task, Func<T, TOut> ok, Func<Error, TOut> fail)
    {
        var r = await task.ConfigureAwait(false);
        return r.Match(ok, fail);
    }

    public static async Task<TOut> MatchAsync<T, TOut>(this Task<Result<T>> task, Func<T, Task<TOut>> ok, Func<Error, Task<TOut>> fail)
    {
        var r = await task.ConfigureAwait(false);
        return r.IsSuccess ? await ok(r.Value).ConfigureAwait(false) : await fail(r.Error).ConfigureAwait(false);
    }

    public static async Task<Result<T>> OrElseAsync<T>(this Task<Result<T>> task, Func<Error, Task<Result<T>>> fallback)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(fallback);
        var r = await task.ConfigureAwait(false);
        return r.IsFailure ? await fallback(r.Error).ConfigureAwait(false) : r;
    }

    public static async Task<Result<T>> TapAsync<T>(this Task<Result<T>> task, Func<T, Task> action)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(action);
        var r = await task.ConfigureAwait(false);
        if (r.IsSuccess)
        {
#pragma warning disable CA1031 // Do not catch general exception types
            try { await action(r.Value).ConfigureAwait(false); } catch { /* ignore side-effect failures */ }
#pragma warning restore CA1031
        }
        return r;
    }

    public static async Task<Result<T>> TapErrorAsync<T>(this Task<Result<T>> task, Action<Error> action)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(action);
        var r = await task.ConfigureAwait(false);
        return r.TapError(action);
    }

    #endregion

    #region Result<T> ↔ async helpers

    public static async Task<Result<TOut>> MapAsync<T, TOut>(this Result<T> r, Func<T, Task<TOut>> map)
        => r.IsFailure ? Results.Failure<TOut>(r.Error) : Results.Success(await map(r.Value).ConfigureAwait(false));

    public static async Task<Result<TOut>> BindAsync<T, TOut>(this Result<T> r, Func<T, Task<Result<TOut>>> bind)
        => r.IsFailure ? Results.Failure<TOut>(r.Error) : await bind(r.Value).ConfigureAwait(false);

    public static async Task<Result<T>> OnSuccessAsync<T>(this Result<T> r, Func<T, Task> action)
    {
        if (r.IsSuccess) await action(r.Value).ConfigureAwait(false);
        return r;
    }

    public static async Task<Result<T>> OnFailureAsync<T>(this Result<T> r, Func<Error, Task> action)
    {
        if (r.IsFailure) await action(r.Error).ConfigureAwait(false);
        return r;
    }

    #endregion

    #region Non-generic Task<Result> helpers

    public static async Task<Result<T>> MapAsync<T>(this Task<Result> task, Func<T> valueFactory)
    {
        var r = await task.ConfigureAwait(false);
        return r.IsFailure ? Results.Failure<T>(r.Error) : Results.Success(valueFactory());
    }

    public static async Task<Result<T>> MapAsync<T>(this Task<Result> task, Func<Task<T>> valueFactory)
    {
        var r = await task.ConfigureAwait(false);
        if (r.IsFailure) return Results.Failure<T>(r.Error);
        var v = await valueFactory().ConfigureAwait(false);
        return Results.Success(v);
    }

    public static async Task<Result<T>> BindAsync<T>(this Task<Result> task, Func<Task<Result<T>>> transform)
    {
        var r = await task.ConfigureAwait(false);
        return r.IsFailure ? Results.Failure<T>(r.Error) : await transform().ConfigureAwait(false);
    }

    public static async Task<Result> OnSuccessAsync(this Task<Result> task, Func<Task> action)
    {
        var r = await task.ConfigureAwait(false);
        if (r.IsSuccess) await action().ConfigureAwait(false);
        return r;
    }

    public static async Task<Result> OnFailureAsync(this Task<Result> task, Func<Error, Task> action)
    {
        var r = await task.ConfigureAwait(false);
        if (r.IsFailure) await action(r.Error).ConfigureAwait(false);
        return r;
    }

    #endregion

    #region Try / Wrap

    public static async Task<Result<T>> ToResultAsync<T>(this Task<T> task, Func<Exception, Error>? map = null)
    {
        try { return Results.Success(await task.ConfigureAwait(false)); }
        catch (Exception ex) { return Results.Failure<T>(map?.Invoke(ex) ?? Error.Internal(ex.Message, exception: ex)); }
    }

    public static async Task<Result> ToResultAsync(this Task task, Func<Exception, Error>? map = null)
    {
        try { await task.ConfigureAwait(false); return Result.Success(); }
        catch (Exception ex) { return Result.Failure(map?.Invoke(ex) ?? Error.Internal(ex.Message, exception: ex)); }
    }

    public static async Task<T?> GetValueOrDefaultAsync<T>(this Task<Result<T>> task, T? defaultValue = default)
    {
        var r = await task.ConfigureAwait(false);
        return r.IsSuccess ? r.Value : defaultValue;
    }

    public static async Task<T> GetValueOrDefaultAsync<T>(this Task<Result<T>> task, Func<Error, T> defaultFactory)
    {
        ArgumentNullException.ThrowIfNull(defaultFactory);
        var r = await task.ConfigureAwait(false);
        return r.IsSuccess ? r.Value : defaultFactory(r.Error);
    }

    public static async Task<T> GetValueOrDefaultAsync<T>(this Task<Result<T>> task, Func<Error, Task<T>> defaultFactory)
    {
        ArgumentNullException.ThrowIfNull(defaultFactory);
        var r = await task.ConfigureAwait(false);
        return r.IsSuccess ? r.Value : await defaultFactory(r.Error).ConfigureAwait(false);
    }

    #endregion

    #region Collections (no dependency on hidden Result.Combine)

    public static Result<T[]> Combine<T>(params Result<T>[] results)
    {
        ArgumentNullException.ThrowIfNull(results);
        if (results.Length == 0) return Array.Empty<T>();
        foreach (var r in results) if (r.IsFailure) return r.Error;
        var vals = new T[results.Length];
        for (int i = 0; i < results.Length; i++) vals[i] = results[i].Value;
        return vals;
    }

    public static async Task<Result<T[]>> CombineAsync<T>(params Task<Result<T>>[] tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return Combine(results);
    }

    public static async Task<Result<T[]>> CombineAsync<T>(IEnumerable<Task<Result<T>>> tasks)
        => Combine(await Task.WhenAll(tasks).ConfigureAwait(false));

    public static Result<TOut[]> Traverse<T, TOut>(this IEnumerable<T> source, Func<T, Result<TOut>> f)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(f);
        var list = new List<TOut>();
        foreach (var item in source)
        {
            var r = f(item);
            if (r.IsFailure) return r.Error;
            list.Add(r.Value);
        }
        return list.ToArray();
    }

    public static async Task<Result<TOut[]>> TraverseAsync<T, TOut>(this IEnumerable<T> source, Func<T, Task<Result<TOut>>> f)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(f);
        var list = new List<TOut>();
        foreach (var item in source)
        {
            var r = await f(item).ConfigureAwait(false);
            if (r.IsFailure) return r.Error;
            list.Add(r.Value);
        }
        return list.ToArray();
    }

    #endregion

    #region IAsyncEnumerable helpers

    public static async IAsyncEnumerable<Result<T>> ToResultsAsync<T>(this IAsyncEnumerable<T> source,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
            yield return Results.Success(item);
    }

    public static async IAsyncEnumerable<TOut> SelectManyResultsAsync<TIn, TOut>(
        this IAsyncEnumerable<TIn> source,
        Func<TIn, Result<TOut>> transform,
        Action<Error>? onError = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(transform);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            var r = transform(item);
            if (r.IsSuccess) yield return r.Value; else onError?.Invoke(r.Error);
        }
    }

    public static async IAsyncEnumerable<TOut> SelectManyResultsAsync<TIn, TOut>(
        this IAsyncEnumerable<TIn> source,
        Func<TIn, Task<Result<TOut>>> transform,
        Action<Error>? onError = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(transform);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            var r = await transform(item).ConfigureAwait(false);
            if (r.IsSuccess) yield return r.Value; else onError?.Invoke(r.Error);
        }
    }

    public static async IAsyncEnumerable<T> WhereSuccessAsync<T>(this IAsyncEnumerable<Result<T>> source,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        await foreach (var r in source.WithCancellation(ct).ConfigureAwait(false))
            if (r.IsSuccess) yield return r.Value;
    }

    public static async IAsyncEnumerable<Error> WhereFailureAsync<T>(this IAsyncEnumerable<Result<T>> source,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        await foreach (var r in source.WithCancellation(ct).ConfigureAwait(false))
            if (r.IsFailure) yield return r.Error;
    }

    public static async Task<Result<IReadOnlyList<T>>> ToListAsync<T>(this IAsyncEnumerable<Result<T>> source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        try
        {
            var values = new List<T>();
            var errors = new List<Error>();
            await foreach (var r in source.WithCancellation(ct).ConfigureAwait(false))
            {
                if (r.IsSuccess) values.Add(r.Value); else errors.Add(r.Error);
            }
            return errors.Count > 0
                ? Results.Failure<IReadOnlyList<T>>(errors.Count == 1 ? errors[0] : Error.Aggregate(errors.ToArray()))
                : Results.Success<IReadOnlyList<T>>(values.AsReadOnly());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Results.Failure<IReadOnlyList<T>>(Error.Cancelled("Operation was cancelled"));
        }
        catch (Exception ex)
        {
            return Results.Failure<IReadOnlyList<T>>(Error.FromException(ex));
        }
    }

    public static async IAsyncEnumerable<IReadOnlyList<T>> BatchAsync<T>(this IAsyncEnumerable<T> source, int batchSize,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (batchSize <= 0) throw new ArgumentException("Batch size must be > 0", nameof(batchSize));

        var batch = new List<T>(batchSize);
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            batch.Add(item);
            if (batch.Count == batchSize)
            {
                yield return batch.AsReadOnly();
                batch.Clear();
            }
        }
        if (batch.Count > 0) yield return batch.AsReadOnly();
    }

    #endregion
}
