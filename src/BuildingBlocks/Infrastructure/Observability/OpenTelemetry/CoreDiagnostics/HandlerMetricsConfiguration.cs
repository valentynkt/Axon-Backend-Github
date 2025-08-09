namespace BuildingBlocks.Infrastructure.Observability.OpenTelemetry.CoreDiagnostics;

/// <summary>
/// Configuration for handler metrics, allowing customization of metric names and descriptions.
/// Follows the Configuration pattern for better maintainability.
/// </summary>
public sealed record HandlerMetricsConfiguration
{
    public required string ActiveCountMetric { get; init; }
    public required string ActiveCountUnit { get; init; }
    public required string ActiveCountDescription { get; init; }
    
    public required string TotalCountMetric { get; init; }
    public required string TotalCountUnit { get; init; }
    public required string TotalCountDescription { get; init; }
    
    public required string SuccessCountMetric { get; init; }
    public required string SuccessCountUnit { get; init; }
    public required string SuccessCountDescription { get; init; }
    
    public required string FailedCountMetric { get; init; }
    public required string FailedCountUnit { get; init; }
    public required string FailedCountDescription { get; init; }
    
    public required string DurationMetric { get; init; }
    public required string DurationUnit { get; init; }
    public required string DurationDescription { get; init; }

    /// <summary>
    /// Creates a configuration for command handler metrics.
    /// </summary>
    public static HandlerMetricsConfiguration ForCommands() => new()
    {
        ActiveCountMetric = TelemetryTags.Metrics.Application.Commands.ActiveCount,
        ActiveCountUnit = "{active_commands}",
        ActiveCountDescription = "Number of commands currently being handled",

        TotalCountMetric = TelemetryTags.Metrics.Application.Commands.TotalExecutedCount,
        TotalCountUnit = "{total_commands}",
        TotalCountDescription = "Total number of executed command that sent to command handlers",

        SuccessCountMetric = TelemetryTags.Metrics.Application.Commands.SuccessCount,
        SuccessCountUnit = "{success_commands}",
        SuccessCountDescription = "Number commands that handled successfully",

        FailedCountMetric = TelemetryTags.Metrics.Application.Commands.FailedCount,
        FailedCountUnit = "{failed_commands}",
        FailedCountDescription = "Number commands that handled with errors",

        DurationMetric = TelemetryTags.Metrics.Application.Commands.HandlerDuration,
        DurationUnit = "s",
        DurationDescription = "Measures the duration of command handler"
    };

    /// <summary>
    /// Creates a configuration for query handler metrics.
    /// </summary>
    public static HandlerMetricsConfiguration ForQueries() => new()
    {
        ActiveCountMetric = TelemetryTags.Metrics.Application.Queries.ActiveCount,
        ActiveCountUnit = "{active_queries}",
        ActiveCountDescription = "Number of queries currently being handled",

        TotalCountMetric = TelemetryTags.Metrics.Application.Queries.TotalExecutedCount,
        TotalCountUnit = "{total_queries}",
        TotalCountDescription = "Total number of executed query that sent to query handlers",

        SuccessCountMetric = TelemetryTags.Metrics.Application.Queries.SuccessCount,
        SuccessCountUnit = "{success_queries}",
        SuccessCountDescription = "Number queries that handled successfully",

        FailedCountMetric = TelemetryTags.Metrics.Application.Queries.FailedCount,
        FailedCountUnit = "{failed_queries}",
        FailedCountDescription = "Number queries that handled with errors",

        DurationMetric = TelemetryTags.Metrics.Application.Queries.HandlerDuration,
        DurationUnit = "s",
        DurationDescription = "Measures the duration of query handler"
    };
}