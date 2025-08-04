namespace Axon.Shared.Common;

/// <summary>
/// Interface for strongly-typed identifiers following SPARC architecture patterns.
/// Uses C# 11+ features for high-performance, compile-time enforced operations.
/// </summary>
/// <typeparam name="TValue">The underlying value type (e.g., Guid, int, string)</typeparam>
public interface IStrongId<TValue> 
    where TValue : struct, IEquatable<TValue>
{
    /// <summary>
    /// Gets the underlying value of the strongly-typed identifier
    /// </summary>
    TValue Value { get; }
}