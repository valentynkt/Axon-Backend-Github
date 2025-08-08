using System.Diagnostics;
using System.Diagnostics.Metrics;
using BuildingBlocks.Infrastructure.Observability.OpenTelemetry.CoreDiagnostics;

namespace BuildingBlocks.Infrastructure.Observability.OpenTelemetry.DiagnosticsProvider;

public interface IDiagnosticsProvider : IDisposable
{
    string InstrumentationName { get; }
    ActivitySource ActivitySource { get; }
    Meter Meter { get; }

    Task ExecuteActivityAsync(
        CreateActivityInfo createActivityInfo,
        Func<Activity?, CancellationToken, Task> action,
        CancellationToken cancellationToken = default
    );

    Task<TResult> ExecuteActivityAsync<TResult>(
        CreateActivityInfo createActivityInfo,
        Func<Activity?, CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default
    );
}