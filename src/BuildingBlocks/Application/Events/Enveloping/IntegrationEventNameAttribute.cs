namespace BuildingBlocks.Application.Events.Enveloping;

/// <summary>
/// Optional: override event type name used in envelopes.
/// Provides stable event names independent of C# type names.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class IntegrationEventNameAttribute : Attribute
{
    public string Name { get; }
    
    public IntegrationEventNameAttribute(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}