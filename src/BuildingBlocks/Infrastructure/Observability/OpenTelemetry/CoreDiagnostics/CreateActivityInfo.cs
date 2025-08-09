using System.Diagnostics;

namespace BuildingBlocks.Infrastructure.Observability.OpenTelemetry.CoreDiagnostics;

public sealed record CreateActivityInfo
{
    public required string Name { get; init; }
    public IDictionary<string, object?> Tags { get; init; } = new Dictionary<string, object?>();
    public string? ParentId { get; init; }
    public ActivityContext? Parent { get; init; }
    public ActivityKind ActivityKind { get; init; } = ActivityKind.Internal;
}