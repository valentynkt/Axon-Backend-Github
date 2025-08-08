using System.Diagnostics;

namespace BuildingBlocks.Infrastructure.Observability.OpenTelemetry.CoreDiagnostics;

public class CreateActivityInfo
{
    public required string Name { get; set; }
    public IDictionary<string, object?> Tags { get; set; } = new Dictionary<string, object?>();
    public string? ParentId { get; set; }
    public ActivityContext? Parent { get; set; }
    public required ActivityKind ActivityKind { get; set; } = ActivityKind.Internal;
}