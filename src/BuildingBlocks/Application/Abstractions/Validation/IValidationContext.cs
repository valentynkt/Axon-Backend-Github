namespace BuildingBlocks.Application.Abstractions.Validation;

public interface IValidationContext
{
    string? TenantId { get; }
    string? UserId { get; }
    string? TraceId { get; }
    IReadOnlyDictionary<string, object> Items { get; }

    T? Get<T>(string key);
}