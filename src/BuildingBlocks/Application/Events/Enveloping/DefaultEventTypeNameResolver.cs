namespace BuildingBlocks.Application.Events.Enveloping;

/// <summary>
/// Default implementation of event type name resolution.
/// Uses IntegrationEventNameAttribute if present, otherwise falls back to type full name.
/// </summary>
public sealed class DefaultEventTypeNameResolver : IEventTypeNameResolver
{
    public string Resolve(Type type)
    {
        var attr = type.GetCustomAttributes(typeof(IntegrationEventNameAttribute), false)
                       .Cast<IntegrationEventNameAttribute>()
                       .FirstOrDefault();
                       
        // Prefer short, stable names. Fallback to FullName to avoid assembly churn.
        return attr?.Name ?? type.FullName ?? type.Name;
    }
}