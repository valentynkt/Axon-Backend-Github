using System.Diagnostics;
using BuildingBlocks.Infrastructure.Observability.OpenTelemetry.DiagnosticsProvider;

namespace BuildingBlocks.Infrastructure.Observability.OpenTelemetry.CoreDiagnostics.Commands;

public sealed class CommandHandlerActivity(IDiagnosticsProvider diagnosticsProvider)
{
    public async Task Execute<TCommand>(
        Func<Activity?, CancellationToken, Task> action,
        CancellationToken cancellationToken
    )
    {
        var activityName = HandlerTypeResolver.CreateCommandActivityName<TCommand>();
        var tags = HandlerTypeResolver.CreateCommandTags<TCommand>();

        await diagnosticsProvider.ExecuteActivityAsync(
            new CreateActivityInfo
            {
                Name = activityName,
                ActivityKind = ActivityKind.Consumer,
                Tags = tags,
            },
            action,
            cancellationToken
        );
    }

    public async Task<TResult> Execute<TCommand, TResult>(
        Func<Activity?, CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken
    )
    {
        var activityName = HandlerTypeResolver.CreateCommandActivityName<TCommand>();
        var tags = HandlerTypeResolver.CreateCommandTags<TCommand>();

        return await diagnosticsProvider.ExecuteActivityAsync(
            new CreateActivityInfo
            {
                Name = activityName,
                ActivityKind = ActivityKind.Consumer,
                Tags = tags,
            },
            action,
            cancellationToken
        );
    }
}