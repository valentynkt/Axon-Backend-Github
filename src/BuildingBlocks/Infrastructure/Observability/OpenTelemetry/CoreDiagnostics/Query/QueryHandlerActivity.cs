using System.Diagnostics;
using BuildingBlocks.Infrastructure.Observability.OpenTelemetry.DiagnosticsProvider;

namespace BuildingBlocks.Infrastructure.Observability.OpenTelemetry.CoreDiagnostics.Query;

public sealed class QueryHandlerActivity(IDiagnosticsProvider diagnosticsProvider)
{
    public async Task<TResult> Execute<TQuery, TResult>(
        Func<Activity?, CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken
    )
    {
        var activityName = HandlerTypeResolver.CreateQueryActivityName<TQuery>();
        var tags = HandlerTypeResolver.CreateQueryTags<TQuery>();

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