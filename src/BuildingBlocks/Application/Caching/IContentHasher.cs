namespace BuildingBlocks.Application.Caching;

/// <summary>
/// Provides hashing functionality for cache key generation.
/// Ensures consistent and collision-resistant hashing of content.
/// </summary>
public interface IContentHasher
{
    /// <summary>
    /// Computes a hash for the given content.
    /// </summary>
    /// <typeparam name="T">The type of content to hash</typeparam>
    /// <param name="content">The content to hash</param>
    /// <returns>A deterministic hash string</returns>
    string ComputeHash<T>(T content) where T : notnull;
}