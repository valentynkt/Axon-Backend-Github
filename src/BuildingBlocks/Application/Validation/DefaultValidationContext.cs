using BuildingBlocks.Application.Abstractions.Validation;

namespace BuildingBlocks.Application.Validation;

public sealed class DefaultValidationContext : IValidationContext
{
    private readonly Dictionary<string, object> _items;

    public string? TenantId { get; }
    public string? UserId { get; }
    public string? TraceId { get; }
    public IReadOnlyDictionary<string, object> Items => _items;

    public DefaultValidationContext(
        string? tenantId = null,
        string? userId = null,
        string? traceId = null,
        IReadOnlyDictionary<string, object>? items = null)
    {
        TenantId = tenantId;
        UserId = userId;
        TraceId = traceId;
        _items = items != null ? new Dictionary<string, object>(items) : new Dictionary<string, object>();
    }

    public T? Get<T>(string key)
    {
        if (string.IsNullOrEmpty(key) || !_items.TryGetValue(key, out var value))
        {
            return default;
        }

        try
        {
            if (value is T directMatch)
            {
                return directMatch;
            }

            var underlyingType = Nullable.GetUnderlyingType(typeof(T));
            if (underlyingType != null)
            {
                if (value == null)
                {
                    return default;
                }
                
                return (T?)Convert.ChangeType(value, underlyingType);
            }

            if (typeof(T).IsEnum && value != null)
            {
                var stringValue = value.ToString();
                if (Enum.TryParse(typeof(T), stringValue, true, out var enumValue))
                {
                    return (T)enumValue;
                }
            }

            if (typeof(T).IsPrimitive || typeof(T) == typeof(decimal))
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }

            return default;
        }
        catch
        {
            return default;
        }
    }
}