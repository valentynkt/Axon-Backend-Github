using BuildingBlocks.Core.Diagnostics.Exceptions;

namespace BuildingBlocks.Core.Diagnostics.Extensions;

public static class ExceptionExtensions
{
    public static Error ToError(this Exception ex, string? source = null)
        => (ex as DomainException)?.Error.WithCorrelationFromActivity()
           ?? Error.FromException(ex).WithCorrelationFromActivity()
               .WithSource(source ?? ex.Source ?? "unknown");

    public static bool IsTransient(this Exception ex)
        => (ex as DomainException)?.Error.IsTransient
           ?? ex is TimeoutException or TaskCanceledException or HttpRequestException;

    public static Exception GetInnermost(this Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        while (ex.InnerException != null) ex = ex.InnerException; return ex;
    }
}