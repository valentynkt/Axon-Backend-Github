namespace BuildingBlocks.Application.Caching;

/// <summary> 
/// Optional: queries can expose tags to help invalidation. 
/// </summary>
public interface ICacheTaggable
{
    string[] CacheTags { get; }
}

/// <summary> 
/// Attribute for declarative query cache tags. 
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CacheTagsAttribute : Attribute
{
    public string[] Tags { get; }
    public CacheTagsAttribute(params string[] tags) => Tags = tags ?? Array.Empty<string>();
}

/// <summary>
/// Commands can declare invalidation tags programmatically.
/// </summary>
public interface ICacheInvalidatable
{
    string[] GetInvalidationTags();
}

/// <summary>
/// Commands can declare invalidation tags via attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class InvalidatesCacheAttribute : Attribute
{
    public string[] Tags { get; }
    public InvalidatesCacheAttribute(params string[] tags) => Tags = tags ?? Array.Empty<string>();
}