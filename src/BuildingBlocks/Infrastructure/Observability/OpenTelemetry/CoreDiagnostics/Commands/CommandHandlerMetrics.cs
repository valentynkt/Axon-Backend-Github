using BuildingBlocks.Infrastructure.Observability.OpenTelemetry.DiagnosticsProvider;

namespace BuildingBlocks.Infrastructure.Observability.OpenTelemetry.CoreDiagnostics.Commands;

/// <summary>
/// Metrics tracking for command handlers.
/// Refactored to eliminate code duplication and follow SOLID principles.
/// </summary>
public sealed class CommandHandlerMetrics : HandlerMetricsBase
{
    public CommandHandlerMetrics(IDiagnosticsProvider diagnosticsProvider)
        : base(diagnosticsProvider, HandlerMetricsConfiguration.ForCommands())
    {
    }

    /// <summary>
    /// Starts tracking metrics for command execution.
    /// </summary>
    public void StartExecuting<TCommand>()
    {
        var commandTags = HandlerTypeResolver.CreateCommandTags<TCommand>();
        var tags = CreateTagList(commandTags);
        StartExecution(tags);
    }

    /// <summary>
    /// Finishes tracking metrics for successful command execution.
    /// </summary>
    public void FinishExecuting<TCommand>()
    {
        var commandTags = HandlerTypeResolver.CreateCommandTags<TCommand>();
        var tags = CreateTagList(commandTags);
        FinishExecution(tags);
    }

    /// <summary>
    /// Records a failed command execution.
    /// </summary>
    public void FailedCommand<TCommand>()
    {
        var commandTags = HandlerTypeResolver.CreateCommandTags<TCommand>();
        var tags = CreateTagList(commandTags);
        RecordFailure(tags);
    }
}