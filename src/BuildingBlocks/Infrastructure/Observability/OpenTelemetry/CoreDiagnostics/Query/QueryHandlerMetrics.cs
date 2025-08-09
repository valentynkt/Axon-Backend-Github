using BuildingBlocks.Infrastructure.Observability.OpenTelemetry.DiagnosticsProvider;

namespace BuildingBlocks.Infrastructure.Observability.OpenTelemetry.CoreDiagnostics.Query;

/// <summary>
/// Metrics tracking for query handlers.
/// Refactored to eliminate code duplication and follow SOLID principles.
/// </summary>
public sealed class QueryHandlerMetrics : HandlerMetricsBase
{
    public QueryHandlerMetrics(IDiagnosticsProvider diagnosticsProvider)
        : base(diagnosticsProvider, HandlerMetricsConfiguration.ForQueries())
    {
    }

    /// <summary>
    /// Starts tracking metrics for query execution.
    /// </summary>
    public void StartExecuting<TQuery>()
    {
        var queryTags = HandlerTypeResolver.CreateQueryTags<TQuery>();
        var tags = CreateTagList(queryTags);
        StartExecution(tags);
    }

    /// <summary>
    /// Finishes tracking metrics for successful query execution.
    /// </summary>
    public void FinishExecuting<TQuery>()
    {
        var queryTags = HandlerTypeResolver.CreateQueryTags<TQuery>();
        var tags = CreateTagList(queryTags);
        FinishExecution(tags);
    }

    /// <summary>
    /// Records a failed query execution.
    /// </summary>
    public void FailedQuery<TQuery>()
    {
        var queryTags = HandlerTypeResolver.CreateQueryTags<TQuery>();
        var tags = CreateTagList(queryTags);
        RecordFailure(tags);
    }
}