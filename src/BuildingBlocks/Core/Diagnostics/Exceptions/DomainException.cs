using System.Runtime.Serialization;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Diagnostics.Exceptions;

/// <summary>
/// Base exception for all domain-related errors.
/// Adds correlation from current Activity and mirrors error metadata into Exception.Data.
/// </summary>
[Serializable]
public class DomainException : Exception
{
    public Error Error { get; }
    public IReadOnlyList<Error> Errors { get; }

    public DomainException(Error error)
        : base((error ?? throw new ArgumentNullException(nameof(error))).Message)
    {
        // Ensure W3C correlation is set and mirror into Data
        Error = error.WithCorrelationFromActivity();
        Errors = new[] { Error };
        MirrorErrorIntoExceptionData(Error);
    }

    public DomainException(string message, Error error)
        : base(message)
    {
        Error = (error ?? throw new ArgumentNullException(nameof(error))).WithCorrelationFromActivity();
        Errors = new[] { Error };
        MirrorErrorIntoExceptionData(Error);
    }

    public DomainException(IEnumerable<Error> errors)
        : base(CreateAggregateMessage(errors))
    {
        var list = (errors ?? throw new ArgumentNullException(nameof(errors))).ToList();
        if (list.Count == 0) throw new ArgumentException("At least one error is required", nameof(errors));

        // Preserve causes via Aggregate factory and attach correlation
        Error = list.Count == 1 ? list[0] : Error.Aggregate(list.ToArray());
        Error = Error.WithCorrelationFromActivity();
        Errors = list;
        MirrorErrorIntoExceptionData(Error);
    }

    public DomainException(string message, IEnumerable<Error> errors)
        : base(message)
    {
        var list = (errors ?? throw new ArgumentNullException(nameof(errors))).ToList();
        if (list.Count == 0) throw new ArgumentException("At least one error is required", nameof(errors));

        Error = list.Count == 1 ? list[0] : Error.Aggregate(list.ToArray());
        Error = Error.WithCorrelationFromActivity();
        Errors = list;
        MirrorErrorIntoExceptionData(Error);
    }

    private static string CreateAggregateMessage(IEnumerable<Error> errors)
    {
        var list = errors.ToList();
        if (list.Count == 0) return "Multiple errors occurred";
        if (list.Count == 1) return list[0].Message;
        return $"Multiple errors occurred: {string.Join("; ", list.Select(e => e.Message))}";
    }

    #pragma warning disable SYSLIB0051
    protected DomainException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        Error = (Error)info.GetValue(nameof(Error), typeof(Error))!;
        Errors = (IReadOnlyList<Error>)info.GetValue(nameof(Errors), typeof(IReadOnlyList<Error>))!;
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(Error), Error);
        info.AddValue(nameof(Errors), Errors);
    }
    #pragma warning restore SYSLIB0051

    private void MirrorErrorIntoExceptionData(Error error)
    {
        try
        {
            Data["ErrorCode"] = error.Code;
            Data["ErrorType"] = error.Type.ToString();
            Data["ErrorSeverity"] = error.Severity.ToString();
            Data["HttpStatusCode"] = error.ToHttpStatusCode();
            Data["OccurredAt"] = error.OccurredAt;
            if (!string.IsNullOrWhiteSpace(error.CorrelationId)) Data["TraceId"] = error.CorrelationId;
            if (!string.IsNullOrWhiteSpace(error.Source)) Data["Source"] = error.Source;

            if (error.Metadata is { Count: > 0 })
            {
                foreach (var (k, v) in error.Metadata) Data[$"Metadata:{k}"] = v ?? "null";
            }
        }
#pragma warning disable CA2200 // Rethrow to preserve stack details
#pragma warning disable CA1031 // Do not catch general exception types  
        catch { /* best-effort only */ }
#pragma warning restore CA1031
#pragma warning restore CA2200
    }
}
