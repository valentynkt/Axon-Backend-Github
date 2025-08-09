namespace BuildingBlocks.Infrastructure.Observability.HealthChecks;

public sealed record HealthOptions
{
    public bool Enabled { get; init; } = true;
}