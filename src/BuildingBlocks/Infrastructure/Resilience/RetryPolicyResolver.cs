using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Resilience;

/// <summary>
/// Default implementation of retry policy resolver.
/// Resolves retry policies based on request type names and configuration.
/// </summary>
public sealed class RetryPolicyResolver : IRetryPolicyResolver
{
    private readonly Dictionary<string, RetryPolicy> _policies;
    private readonly RetryOptions _options;

    public RetryPolicyResolver(IOptions<RetryOptions> options)
    {
        _options = options.Value;
        _policies = InitializeDefaultPolicies();
    }

    public RetryPolicy? GetPolicy(string requestTypeName)
    {
        // Try exact type name match first
        if (_policies.TryGetValue(requestTypeName, out var policy))
        {
            return policy;
        }

        // Try pattern matching for common request types
        if (requestTypeName.EndsWith("Command"))
        {
            return _policies.GetValueOrDefault("DefaultCommand");
        }

        if (requestTypeName.EndsWith("Query"))
        {
            return _policies.GetValueOrDefault("DefaultQuery");
        }

        // Return default policy if configured
        return _policies.GetValueOrDefault("Default");
    }

    private Dictionary<string, RetryPolicy> InitializeDefaultPolicies()
    {
        return new Dictionary<string, RetryPolicy>
        {
            ["Default"] = new RetryPolicy
            {
                MaxAttempts = _options.DefaultMaxAttempts,
                InitialDelay = _options.DefaultInitialDelay,
                MaxDelay = _options.DefaultMaxDelay,
                UseCircuitBreaker = false
            },
            
            ["DefaultCommand"] = RetryPolicy.ForCommands,
            
            ["DefaultQuery"] = RetryPolicy.ForQueries,
            
            // Example specific policies
            ["ProcessPaymentCommand"] = new RetryPolicy
            {
                MaxAttempts = 5,
                InitialDelay = TimeSpan.FromMilliseconds(200),
                MaxDelay = TimeSpan.FromSeconds(30),
                UseCircuitBreaker = true,
                CircuitBreakerThreshold = 3,
                CircuitBreakerDuration = TimeSpan.FromMinutes(1)
            },
            
            ["SendEmailCommand"] = new RetryPolicy
            {
                MaxAttempts = 3,
                InitialDelay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(15),
                UseCircuitBreaker = true
            }
        };
    }
}