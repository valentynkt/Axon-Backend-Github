namespace BuildingBlocks.Infrastructure.Resilience;

/// <summary>
/// Resolves retry policies for request types.
/// </summary>
public interface IRetryPolicyResolver
{
    /// <summary>
    /// Gets a retry policy for the specified request type name.
    /// </summary>
    /// <param name="requestTypeName">The type name of the request</param>
    /// <returns>The retry policy if found; otherwise null</returns>
    RetryPolicy? GetPolicy(string requestTypeName);
}