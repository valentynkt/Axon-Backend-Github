using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.Core.Diagnostics.Performance.Extensions;

/// <summary>
/// Minimal registration for error telemetry.
/// Keeps things simple: exposes IErrorTelemetry as a singleton and lets your
/// OpenTelemetry / logging pipeline do the heavy lifting.
/// </summary>
public static class ErrorPerformanceExtensions
{
    /// <summary>
    /// Registers the minimal error telemetry.
    /// </summary>
    public static IServiceCollection AddErrorTelemetry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IErrorTelemetry, ErrorTelemetry>();
        return services;
    }
}