using System.Diagnostics;

namespace BuildingBlocks.Infrastructure.Observability.OpenTelemetry.CoreDiagnostics;

public sealed record ActivityInfo
{
    public required string Name { get; init; }
    public DateTime StartTime { get; init; }
    public TimeSpan Duration { get; init; }
    public required string Status { get; init; }
    public string? StatusDescription { get; init; }
    public IDictionary<string, string?> Tags { get; init; } = new Dictionary<string, string?>();
    public IList<ActivityEventInfo> Events { get; init; } = new List<ActivityEventInfo>();
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public string? ParentId { get; init; }
    public ActivityContext? Parent { get; init; }
    public ActivityKind Kind { get; init; }
}

public sealed record ActivityEventInfo
{
    public required string Name { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public IDictionary<string, object?> Attributes { get; init; } = new Dictionary<string, object?>();
}