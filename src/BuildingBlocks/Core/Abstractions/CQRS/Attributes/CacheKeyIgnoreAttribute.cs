namespace BuildingBlocks.Core.Abstractions.CQRS.Attributes;

/// <summary>
/// Marks a property to be excluded from cache key generation.
/// Useful for volatile fields like RequestId, RequestedAt, TraceId, etc.
/// that should not affect cache key uniqueness.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class CacheKeyIgnoreAttribute : Attribute
{
}