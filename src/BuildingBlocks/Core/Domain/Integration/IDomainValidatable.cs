using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;

namespace BuildingBlocks.Core.Domain.Integration;

/// <summary>
/// Marker interface for CQRS commands/queries that should be validated using domain business rules.
/// This integrates Epic 2 domain validation with Epic 5 ValidationBehavior.
/// </summary>
public interface IDomainValidatable
{
    /// <summary>
    /// Validate the request using domain business rules.
    /// This method is called by the ValidationBehavior in Epic 5.
    /// </summary>
    /// <returns>Validation result with accumulated errors</returns>
    Validation<Unit> ValidateDomainRules();
}

/// <summary>
/// Marker interface for CQRS commands/queries that need domain business rule validation with async dependencies.
/// This supports validation that requires database access or external services.
/// </summary>
public interface IDomainValidatableAsync
{
    /// <summary>
    /// Validate the request using domain business rules asynchronously.
    /// This method is called by the ValidationBehavior in Epic 5.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with accumulated errors</returns>
    Task<Validation<Unit>> ValidateDomainRulesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for commands that work with aggregates and may produce domain events.
/// This integrates with Epic 5 TransactionBehavior for proper event dispatch.
/// </summary>
public interface IDomainCommand : IDomainValidatable
{
    /// <summary>
    /// Get the aggregate root type this command operates on.
    /// Used by TransactionBehavior for transaction scoping and event dispatch.
    /// </summary>
    Type GetAggregateType();
}

/// <summary>
/// Interface for commands that work with aggregates and may produce domain events asynchronously.
/// This integrates with Epic 5 TransactionBehavior for proper event dispatch.
/// </summary>
public interface IDomainCommandAsync : IDomainValidatableAsync
{
    /// <summary>
    /// Get the aggregate root type this command operates on.
    /// Used by TransactionBehavior for transaction scoping and event dispatch.
    /// </summary>
    Type GetAggregateType();
}

/// <summary>
/// Interface for queries that can be cached using domain-specific cache keys.
/// This integrates with Epic 5 CachingBehavior for intelligent cache management.
/// </summary>
public interface ICacheableQuery
{
    /// <summary>
    /// Generate a cache key for this query.
    /// Should include all parameters that affect the result.
    /// </summary>
    /// <returns>A unique cache key for this query instance</returns>
    string GetCacheKey();
    
    /// <summary>
    /// Get the cache duration for this query.
    /// Allows per-query-type cache policy configuration.
    /// </summary>
    /// <returns>Cache duration, or null for default duration</returns>
    TimeSpan? GetCacheDuration() => null;
    
    /// <summary>
    /// Get cache tags for invalidation purposes.
    /// Used by CachingBehavior for intelligent cache invalidation.
    /// </summary>
    /// <returns>Collection of cache tags</returns>
    IEnumerable<string> GetCacheTags() => Array.Empty<string>();
}

/// <summary>
/// Interface for commands/queries that should be retried on transient failures.
/// This integrates with Epic 5 RetryBehavior for resilient operations.
/// </summary>
public interface IRetryableOperation
{
    /// <summary>
    /// Get the retry policy name for this operation.
    /// Used by RetryBehavior to select appropriate retry configuration.
    /// </summary>
    /// <returns>Retry policy name, or null for default policy</returns>
    string? GetRetryPolicyName() => null;
    
    /// <summary>
    /// Determine if an exception should trigger a retry.
    /// Allows operation-specific transient error detection.
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <returns>True if the operation should be retried</returns>
    bool ShouldRetry(Exception exception) => IsTransientException(exception);
    
    /// <summary>
    /// Default transient exception detection logic.
    /// Can be overridden for custom transient error detection.
    /// </summary>
    /// <param name="exception">The exception to check</param>
    /// <returns>True if the exception is considered transient</returns>
    protected static bool IsTransientException(Exception exception)
    {
        return exception is TimeoutException
            or OperationCanceledException
            or HttpRequestException;
    }
}

/// <summary>
/// Interface for operations that require specific authorization context.
/// This integrates with Epic 5 AuthorizationBehavior for declarative security.
/// </summary>
public interface IAuthorizableOperation
{
    /// <summary>
    /// Get the required permissions for this operation.
    /// Used by AuthorizationBehavior to check user permissions.
    /// </summary>
    /// <returns>Collection of required permission names</returns>
    IEnumerable<string> GetRequiredPermissions();
    
    /// <summary>
    /// Get the resource identifier for resource-based authorization.
    /// Used for operations that require access to specific resources.
    /// </summary>
    /// <returns>Resource identifier, or null if not applicable</returns>
    string? GetResourceId() => null;
    
    /// <summary>
    /// Get the resource type for resource-based authorization.
    /// Used for operations that require access to specific resource types.
    /// </summary>
    /// <returns>Resource type, or null if not applicable</returns>
    string? GetResourceType() => null;
}

/// <summary>
/// Base class for domain commands that provides common integration patterns.
/// Implements standard interfaces for Epic 5 pipeline behavior integration.
/// </summary>
public abstract record DomainCommandBase : IDomainCommand
{
    /// <summary>
    /// Validate the command using domain business rules.
    /// Override this method to provide command-specific validation.
    /// </summary>
    /// <returns>Validation result</returns>
    public virtual Validation<Unit> ValidateDomainRules()
    {
        return Validation<Unit>.Valid(Unit.Value);
    }
    
    /// <summary>
    /// Get the aggregate root type this command operates on.
    /// Must be implemented by derived classes.
    /// </summary>
    /// <returns>Aggregate root type</returns>
    public abstract Type GetAggregateType();
}

/// <summary>
/// Base class for domain queries that provides common integration patterns.
/// Implements standard interfaces for Epic 5 pipeline behavior integration.
/// </summary>
public abstract record DomainQueryBase : IDomainValidatable
{
    /// <summary>
    /// Validate the query using domain business rules.
    /// Override this method to provide query-specific validation.
    /// </summary>
    /// <returns>Validation result</returns>
    public virtual Validation<Unit> ValidateDomainRules()
    {
        return Validation<Unit>.Valid(Unit.Value);
    }
}

/// <summary>
/// Base class for cacheable domain queries.
/// Provides default cache key generation and common caching patterns.
/// </summary>
public abstract record CacheableDomainQueryBase : DomainQueryBase, ICacheableQuery
{
    /// <summary>
    /// Generate a cache key for this query.
    /// Default implementation uses type name and property hash.
    /// Override for custom cache key generation.
    /// </summary>
    /// <returns>Cache key</returns>
    public virtual string GetCacheKey()
    {
        var typeName = GetType().Name;
        var propertiesHash = GetHashCode();
        return $"{typeName}:{propertiesHash:X8}";
    }
    
    /// <summary>
    /// Get the cache duration for this query.
    /// Override to provide query-specific cache duration.
    /// </summary>
    /// <returns>Cache duration</returns>
    public virtual TimeSpan? GetCacheDuration() => null;
    
    /// <summary>
    /// Get cache tags for invalidation purposes.
    /// Override to provide query-specific cache tags.
    /// </summary>
    /// <returns>Cache tags</returns>
    public virtual IEnumerable<string> GetCacheTags() => Array.Empty<string>();
}