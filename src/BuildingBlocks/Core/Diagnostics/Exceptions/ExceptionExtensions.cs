using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using BuildingBlocks.Core.Diagnostics.Errors;
// Optional (uncomment if you want Result<T> conversion here):
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Diagnostics.Extensions;

/// <summary>
/// State-of-the-art exception helpers: mapping, flattening, retryability,
/// correlation, RFC7807, and conversion to Result.
/// </summary>
public static class ExceptionExtensions
{
    /// <summary>
    /// Map an Exception to rich Error (uses Error.FromException + W3C trace correlation).
    /// </summary>
    public static Error ToError(this Exception exception, string? source = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var error = Error.FromException(exception)
                         .WithCorrelationFromActivity();

        if (!string.IsNullOrWhiteSpace(source))
            error = error.WithSource(source);

        // Copy exception.Data (non-sensitive) into metadata once
        if (exception.Data?.Count > 0)
        {
            var meta = new Dictionary<string, object>();
            foreach (var key in exception.Data.Keys.Cast<object>())
            {
                if (key is null) continue;
                var k = key.ToString() ?? "UnknownKey";
                meta[k] = exception.Data[key] ?? "null";
            }

            if (meta.Count > 0)
                error = error.WithMetadata(meta);
        }

        return error;
    }

    /// <summary>
    /// Convert Exception to RFC7807-like Problem via Error mapping.
    /// </summary>
    public static Error.Problem ToProblem(this Exception exception, string? instance = null, string? type = null, string? title = null)
        => exception.ToError().ToProblem(instance, type, title);

    /// <summary>
    /// Convert Exception to failure Result&lt;T&gt; (handy for pipelines).
    /// </summary>
    public static Result<T> ToFailureResult<T>(this Exception exception, string? source = null)
        => Result<T>.Failure(exception.ToError(source));

    /// <summary>
    /// Best-effort HTTP status from Exception (DomainException wins; else Error.FromException).
    /// </summary>
    public static int ToHttpStatusCode(this Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is BuildingBlocks.Core.Diagnostics.Exceptions.DomainException de)
            return de.Error.ToHttpStatusCode();

        return Error.FromException(exception).ToHttpStatusCode();
    }

    /// <summary>
    /// Is this exception likely transient (good candidate for retry)?
    /// </summary>
    public static bool IsTransient(this Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // DomainException uses Error hints
        if (exception is BuildingBlocks.Core.Diagnostics.Exceptions.DomainException de)
            return de.Error.IsTransient;

        // Broad heuristics
        return exception switch
        {
            TimeoutException => true,
            TaskCanceledException tce when tce.CancellationToken.IsCancellationRequested == false => true, // request timeout pattern
            OperationCanceledException oce when oce.CancellationToken.IsCancellationRequested == false => true,
            HttpRequestException httpEx when httpEx.StatusCode is HttpStatusCode.RequestTimeout
                                              or HttpStatusCode.GatewayTimeout
                                              or HttpStatusCode.BadGateway
                                              or HttpStatusCode.ServiceUnavailable
                                              or null => true,
            SocketException => true,
            System.Net.NetworkInformation.NetworkInformationException => true,
            _ => false
        };
    }

    /// <summary>
    /// Should we retry? (transient AND not security/validation/business input).
    /// </summary>
    public static bool IsRetryable(this Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is BuildingBlocks.Core.Diagnostics.Exceptions.DomainException de)
            return de.Error.IsRetryable;

        if (exception.IsTransient()) return true;

        // Explicitly avoid retry on client/input/security
        var status = exception.ToHttpStatusCode();
        if (status is (int)HttpStatusCode.Unauthorized
                or (int)HttpStatusCode.Forbidden
                or (int)HttpStatusCode.BadRequest
                or 422
                or 409
                or 412)
            return false;

        return false;
    }

    /// <summary>
    /// Returns the deepest inner exception (root cause).
    /// </summary>
    public static Exception GetInnermost(this Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        while (exception.InnerException is not null) exception = exception.InnerException;
        return exception;
    }

    /// <summary>
    /// Flatten exception chain, including AggregateException trees.
    /// </summary>
    public static IEnumerable<Exception> Flatten(this Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var stack = new Stack<Exception>();
        stack.Push(exception);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            if (current is AggregateException ae)
            {
                foreach (var inner in ae.InnerExceptions)
                    stack.Push(inner);
            }
            else if (current.InnerException is not null)
            {
                stack.Push(current.InnerException);
            }
        }
    }

    /// <summary>
    /// A concise, safe message for logging (includes root cause).
    /// </summary>
    public static string SafeSummary(this Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var root = exception.GetInnermost();
        return $"{exception.GetType().Name}: {exception.Message} | Root: {root.GetType().Name}: {root.Message}";
    }

    /// <summary>
    /// Attach key/value into Exception.Data safely (fluent).
    /// </summary>
    public static TException WithData<TException>(this TException exception, string key, object? value)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(key);
#pragma warning disable CA1031 // Do not catch general exception types
        try { exception.Data[key] = value ?? "null"; } catch { /* ignore */ }
#pragma warning restore CA1031
        return exception;
    }

    /// <summary>
    /// Attach the current W3C TraceId as "TraceId" in Exception.Data.
    /// </summary>
    public static TException WithCorrelationFromActivity<TException>(this TException exception)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(exception);
        var traceId = Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrWhiteSpace(traceId))
            exception.WithData("TraceId", traceId);
        return exception;
    }

    /// <summary>
    /// If the exception is (or wraps) a DomainException, returns its Error; otherwise maps via Error.FromException().
    /// </summary>
    public static Error ExtractOrMapError(this Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var domain = exception.Flatten()
                              .OfType<BuildingBlocks.Core.Diagnostics.Exceptions.DomainException>()
                              .FirstOrDefault();
        return (domain?.Error ?? Error.FromException(exception))
               .WithCorrelationFromActivity();
    }

    /// <summary>
    /// Build a -very- compact JSON for structured logs (no stack traces).
    /// </summary>
    public static string ToCompactJson(this Exception exception)
    {
        var err = exception.ExtractOrMapError().Redact();
        var payload = new
        {
            error = err.Code,
            message = err.Message,
            type = err.Type.ToString(),
            severity = err.Severity.ToString(),
            status = err.ToHttpStatusCode(),
            traceId = err.CorrelationId,
            source = err.Source
        };
        return JsonSerializer.Serialize(payload);
    }
}
