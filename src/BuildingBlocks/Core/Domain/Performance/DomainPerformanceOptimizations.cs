using System.Collections.Concurrent;
using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Core.Domain.Specifications;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;

namespace BuildingBlocks.Core.Domain.Performance;

/// <summary>
/// Epic 2 performance optimization patterns for domain models.
/// Provides caching, lazy evaluation, and efficient validation strategies.
/// </summary>

#region Business Rule Performance Optimizations

/// <summary>
/// Cached business rule evaluator to prevent repeated expensive rule evaluations.
/// Uses weak references to prevent memory leaks while maintaining performance.
/// </summary>
public sealed class CachedBusinessRuleEvaluator
{
    private readonly ConcurrentDictionary<string, WeakReference<CachedRuleResult>> _ruleCache = new();
    private readonly TimeSpan _cacheExpiration;

    public CachedBusinessRuleEvaluator(TimeSpan? cacheExpiration = null)
    {
        _cacheExpiration = cacheExpiration ?? TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Evaluates business rule with caching support.
    /// Cache key is generated from rule type and input hash.
    /// </summary>
    public bool EvaluateRule<T>(IBusinessRule rule, T context, Func<IBusinessRule, T, bool> evaluator)
    {
        var cacheKey = GenerateCacheKey(rule, context);
        
        // Check cache first
        if (_ruleCache.TryGetValue(cacheKey, out var weakRef) && 
            weakRef.TryGetTarget(out var cachedResult) &&
            !cachedResult.IsExpired(_cacheExpiration))
        {
            return cachedResult.Result;
        }

        // Evaluate rule
        var result = evaluator(rule, context);
        
        // Cache result
        var newCachedResult = new CachedRuleResult(result, DateTime.UtcNow);
        _ruleCache.AddOrUpdate(cacheKey, 
            new WeakReference<CachedRuleResult>(newCachedResult),
            (key, existing) => new WeakReference<CachedRuleResult>(newCachedResult));

        return result;
    }

    private string GenerateCacheKey<T>(IBusinessRule rule, T context)
    {
        var ruleKey = $"{rule.GetType().FullName}:{rule.Code}";
        var contextKey = context?.GetHashCode().ToString("X8") ?? "null";
        return $"{ruleKey}:{contextKey}";
    }

    /// <summary>
    /// Clears expired cache entries to prevent memory leaks.
    /// Should be called periodically.
    /// </summary>
    public void ClearExpiredEntries()
    {
        var expiredKeys = new List<string>();
        
        foreach (var kvp in _ruleCache)
        {
            if (!kvp.Value.TryGetTarget(out var result) || 
                result.IsExpired(_cacheExpiration))
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            _ruleCache.TryRemove(key, out _);
        }
    }
}

/// <summary>
/// Cached result for business rule evaluation.
/// </summary>
internal sealed record CachedRuleResult(bool Result, DateTime EvaluatedAt)
{
    public bool IsExpired(TimeSpan expiration) => DateTime.UtcNow - EvaluatedAt > expiration;
}

#endregion

#region Specification Performance Optimizations

/// <summary>
/// Compiled specification cache for improved query performance.
/// Pre-compiles frequently used specifications to avoid repeated compilation overhead.
/// </summary>
public sealed class CompiledSpecificationCache<T>
{
    private readonly ConcurrentDictionary<string, Func<T, bool>> _compiledSpecifications = new();
    private readonly ConcurrentDictionary<string, DateTime> _compilationTimes = new();
    private readonly TimeSpan _cacheExpiration;

    public CompiledSpecificationCache(TimeSpan? cacheExpiration = null)
    {
        _cacheExpiration = cacheExpiration ?? TimeSpan.FromHours(1);
    }

    /// <summary>
    /// Gets compiled specification with caching.
    /// </summary>
    public Func<T, bool> GetCompiledSpecification(Specification<T> specification)
    {
        var cacheKey = GetSpecificationCacheKey(specification);
        
        // Check if we have a cached compiled version
        if (_compiledSpecifications.TryGetValue(cacheKey, out var compiled) &&
            _compilationTimes.TryGetValue(cacheKey, out var compiledAt) &&
            DateTime.UtcNow - compiledAt < _cacheExpiration)
        {
            return compiled;
        }

        // Compile and cache
        var expression = specification.ToExpression();
        var compiledFunc = expression.Compile();
        
        _compiledSpecifications.AddOrUpdate(cacheKey, compiledFunc, (key, existing) => compiledFunc);
        _compilationTimes.AddOrUpdate(cacheKey, DateTime.UtcNow, (key, existing) => DateTime.UtcNow);
        
        return compiledFunc;
    }

    private string GetSpecificationCacheKey(Specification<T> specification)
    {
        // Use specification type and expression string as cache key
        var specType = specification.GetType().FullName;
        var expressionString = specification.ToExpression().ToString();
        
        // Create hash to keep key length manageable
        var combined = $"{specType}:{expressionString}";
        return combined.GetHashCode().ToString("X8");
    }
}

/// <summary>
/// Specification composition optimizer that reduces redundant evaluations.
/// Optimizes AND/OR chains and eliminates contradictory conditions.
/// </summary>
public static class SpecificationOptimizer
{
    /// <summary>
    /// Optimizes specification chain by eliminating redundant conditions.
    /// </summary>
    public static Specification<T> Optimize<T>(Specification<T> specification)
    {
        // For now, return as-is. In a full implementation, this would:
        // 1. Analyze the expression tree
        // 2. Eliminate contradictory conditions (e.g., x > 5 AND x < 3)
        // 3. Combine redundant conditions (e.g., x > 5 AND x > 3 -> x > 5)
        // 4. Reorder conditions by selectivity (most restrictive first)
        
        return specification;
    }

    /// <summary>
    /// Estimates the selectivity of a specification for query optimization.
    /// Returns a value between 0 (very selective) and 1 (not selective).
    /// </summary>
    public static double EstimateSelectivity<T>(Specification<T> specification)
    {
        // Simplified selectivity estimation
        // In reality, this would use statistics or heuristics
        
        var expressionString = specification.ToExpression().ToString();
        
        // Heuristic: specifications with equality checks are more selective
        if (expressionString.Contains("=="))
            return 0.1;
            
        // Range checks are moderately selective
        if (expressionString.Contains(">") || expressionString.Contains("<"))
            return 0.3;
            
        // Contains/StartsWith are less selective
        if (expressionString.Contains("Contains") || expressionString.Contains("StartsWith"))
            return 0.5;
            
        // Default selectivity
        return 0.7;
    }
}

#endregion

#region Aggregate Performance Optimizations

/// <summary>
/// Lazy-loaded aggregate invariants to improve performance for large aggregates.
/// Invariants are only evaluated when explicitly requested or during validation.
/// </summary>
public abstract class PerformantAggregateRoot<TId> : AggregateRoot<TId>
    where TId : IStrongId
{
    private readonly Lazy<IEnumerable<IBusinessRule>> _lazyInvariants;
    private readonly object _validationLock = new();
    private DateTime? _lastValidation;
    private Validation<Unit>? _cachedValidation;
    private readonly TimeSpan _validationCacheExpiry = TimeSpan.FromMinutes(1);

    protected PerformantAggregateRoot()
    {
        _lazyInvariants = new Lazy<IEnumerable<IBusinessRule>>(GetInvariantsCore);
    }

    /// <summary>
    /// Cached validation to prevent repeated expensive validations.
    /// </summary>
    public override Validation<Unit> Validate()
    {
        lock (_validationLock)
        {
            // Return cached validation if still valid
            if (_cachedValidation != null && 
                _lastValidation.HasValue && 
                DateTime.UtcNow - _lastValidation.Value < _validationCacheExpiry)
            {
                return _cachedValidation;
            }

            // Perform validation
            var validation = base.Validate();
            
            // Cache result
            _cachedValidation = validation;
            _lastValidation = DateTime.UtcNow;
            
            return validation;
        }
    }

    /// <summary>
    /// Invalidates validation cache when aggregate state changes.
    /// Call this in methods that modify aggregate state.
    /// </summary>
    protected void InvalidateValidationCache()
    {
        lock (_validationLock)
        {
            _cachedValidation = null;
            _lastValidation = null;
        }
    }

    /// <summary>
    /// Lazy-loaded invariants for better performance.
    /// </summary>
    protected override IEnumerable<IBusinessRule> GetInvariants()
    {
        return _lazyInvariants.Value;
    }

    /// <summary>
    /// Override this method instead of GetInvariants() for lazy loading.
    /// </summary>
    protected abstract IEnumerable<IBusinessRule> GetInvariantsCore();

    /// <summary>
    /// Optimized state change application with validation caching.
    /// </summary>
    protected override Result<T> ApplyChange<T>(Func<T> stateChangeFunc)
    {
        InvalidateValidationCache();
        return base.ApplyChange(stateChangeFunc);
    }
}

#endregion

#region Value Object Performance Optimizations

/// <summary>
/// Cached value object validation for expensive validation operations.
/// Particularly useful for value objects with external dependencies or complex calculations.
/// </summary>
public abstract class PerformantValueObject : ValueObject
{
    private static readonly ConcurrentDictionary<string, WeakReference<CachedValidationResult>> ValidationCache = new();
    private readonly Lazy<string> _lazyHashCode;

    protected PerformantValueObject()
    {
        _lazyHashCode = new Lazy<string>(ComputeHashCode);
    }

    /// <summary>
    /// Cached validation with configurable expiration.
    /// </summary>
    public override Validation<Unit> Validate()
    {
        var cacheKey = GetValidationCacheKey();
        var expiration = GetValidationCacheExpiration();
        
        // Check cache
        if (ValidationCache.TryGetValue(cacheKey, out var weakRef) &&
            weakRef.TryGetTarget(out var cachedResult) &&
            !cachedResult.IsExpired(expiration))
        {
            return cachedResult.ValidationResult;
        }

        // Perform validation
        var validation = ValidateCore();
        
        // Cache result
        var newCachedResult = new CachedValidationResult(validation, DateTime.UtcNow);
        ValidationCache.AddOrUpdate(cacheKey,
            new WeakReference<CachedValidationResult>(newCachedResult),
            (key, existing) => new WeakReference<CachedValidationResult>(newCachedResult));

        return validation;
    }

    /// <summary>
    /// Override this method to provide the actual validation logic.
    /// </summary>
    protected abstract Validation<Unit> ValidateCore();

    /// <summary>
    /// Override this to provide custom cache key generation.
    /// Default implementation uses the hash code of equality components.
    /// </summary>
    protected virtual string GetValidationCacheKey()
    {
        return $"{GetType().FullName}:{_lazyHashCode.Value}";
    }

    /// <summary>
    /// Override this to customize cache expiration.
    /// Default is 5 minutes.
    /// </summary>
    protected virtual TimeSpan GetValidationCacheExpiration()
    {
        return TimeSpan.FromMinutes(5);
    }

    private string ComputeHashCode()
    {
        var components = GetEqualityComponents().ToArray();
        var hash = components.Aggregate(0, (current, obj) => current ^ (obj?.GetHashCode() ?? 0));
        return hash.ToString("X8");
    }

    /// <summary>
    /// Clears expired validation cache entries.
    /// Should be called periodically by a background service.
    /// </summary>
    public static void ClearExpiredValidationCache()
    {
        var expiredKeys = new List<string>();
        var now = DateTime.UtcNow;
        
        foreach (var kvp in ValidationCache)
        {
            if (!kvp.Value.TryGetTarget(out var result) || 
                now - result.ValidatedAt > TimeSpan.FromHours(1)) // Max cache time
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            ValidationCache.TryRemove(key, out _);
        }
    }
}

/// <summary>
/// Cached validation result for value objects.
/// </summary>
internal sealed record CachedValidationResult(Validation<Unit> ValidationResult, DateTime ValidatedAt)
{
    public bool IsExpired(TimeSpan expiration) => DateTime.UtcNow - ValidatedAt > expiration;
}

#endregion

#region Performance Monitoring

/// <summary>
/// Performance metrics collector for Epic 2 domain operations.
/// Provides insights into domain model performance characteristics.
/// </summary>
public interface IDomainPerformanceCollector
{
    void RecordBusinessRuleEvaluation(string ruleType, TimeSpan duration, bool passed);
    void RecordSpecificationExecution(string specificationType, int resultCount, TimeSpan duration);
    void RecordAggregateValidation(string aggregateType, TimeSpan duration, bool passed);
    void RecordValueObjectValidation(string valueObjectType, TimeSpan duration, bool passed);
    
    Task<DomainPerformanceReport> GenerateReportAsync(TimeSpan period);
}

/// <summary>
/// Domain performance report for monitoring and optimization.
/// </summary>
public sealed record DomainPerformanceReport
{
    public TimeSpan ReportPeriod { get; init; }
    public Dictionary<string, BusinessRuleMetrics> BusinessRuleMetrics { get; init; } = new();
    public Dictionary<string, SpecificationMetrics> SpecificationMetrics { get; init; } = new();
    public Dictionary<string, AggregateMetrics> AggregateMetrics { get; init; } = new();
    public Dictionary<string, ValueObjectMetrics> ValueObjectMetrics { get; init; } = new();
    public List<string> PerformanceRecommendations { get; init; } = new();
}

public sealed record BusinessRuleMetrics
{
    public int TotalEvaluations { get; init; }
    public TimeSpan AverageExecutionTime { get; init; }
    public TimeSpan MaxExecutionTime { get; init; }
    public double PassRate { get; init; }
    public int CacheHitRate { get; init; }
}

public sealed record SpecificationMetrics
{
    public int TotalExecutions { get; init; }
    public TimeSpan AverageExecutionTime { get; init; }
    public double AverageResultCount { get; init; }
    public int CompilationCacheHitRate { get; init; }
}

public sealed record AggregateMetrics
{
    public int TotalValidations { get; init; }
    public TimeSpan AverageValidationTime { get; init; }
    public double ValidationPassRate { get; init; }
    public int ValidationCacheHitRate { get; init; }
}

public sealed record ValueObjectMetrics
{
    public int TotalValidations { get; init; }
    public TimeSpan AverageValidationTime { get; init; }
    public double ValidationPassRate { get; init; }
    public int ValidationCacheHitRate { get; init; }
}

#endregion