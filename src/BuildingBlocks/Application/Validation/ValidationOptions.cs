namespace BuildingBlocks.Application.Validation;

public sealed class ValidationOptions
{
    public bool EnableCaching { get; init; } = false;
    public TimeSpan CacheTtl { get; init; } = TimeSpan.FromSeconds(30);
    public bool GroupByField { get; init; } = true;
}