using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Core.Domain.CQRS;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Application.Configuration;

/// <summary>
/// Epic 2 + Epic 5 integrated pipeline configuration.
/// Demonstrates proper registration and ordering of all pipeline behaviors
/// for comprehensive domain-driven development with functional patterns.
/// </summary>
public static class Epic2Epic5PipelineConfiguration
{
    /// <summary>
    /// Registers the complete Epic 2 + Epic 5 pipeline with proper behavior ordering.
    /// 
    /// Pipeline Execution Order (CRITICAL - order matters!):
    /// 1. ObservabilityBehavior - Outermost: telemetry, tracing, metrics
    /// 2. LoggingBehavior - Structured logging with correlation IDs  
    /// 3. ValidationBehavior - FluentValidation structural validation
    /// 4. DomainValidationBehavior - Epic 2 domain business rules validation
    /// 5. CachingBehavior - Query caching (skipped for commands)
    /// 6. RetryBehavior - Resilience with retries for transient failures
    /// 7. TransactionBehavior - Innermost: database transactions + domain event dispatch
    /// 8. Handler execution with Epic 2 domain patterns
    /// </summary>
    public static IServiceCollection AddEpic2Epic5Pipeline(this IServiceCollection services)
    {
        // Register MediatR with all assemblies containing handlers
        services.AddMediatR(cfg =>
        {
            // Register from multiple assemblies
            cfg.RegisterServicesFromAssembly(typeof(Epic2Epic5PipelineConfiguration).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(DomainCommandBase).Assembly);
            
            // Register pipeline behaviors in EXECUTION ORDER (innermost to outermost)
            // MediatR executes behaviors in reverse registration order!
            
            // 7. TransactionBehavior (innermost - closest to handler)
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
            
            // 6. RetryBehavior  
            cfg.AddOpenBehavior(typeof(RetryBehavior<,>));
            
            // 5. CachingBehavior
            cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
            
            // 4. DomainValidationBehavior (Epic 2 domain rules)
            cfg.AddOpenBehavior(typeof(DomainValidationBehavior<,>));
            
            // 3. ValidationBehavior (FluentValidation structural validation)
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            
            // 2. LoggingBehavior
            cfg.AddOpenBehavior(typeof(ResultLoggingBehavior<,>));
            
            // 1. ObservabilityBehavior (outermost - first to execute)
            cfg.AddOpenBehavior(typeof(ObservabilityPipelineBehavior<,>));
        });

        // Register FluentValidation
        services.AddValidatorsFromAssembly(typeof(Epic2Epic5PipelineConfiguration).Assembly);

        return services;
    }

    /// <summary>
    /// Registers Epic 2 domain services and repositories.
    /// </summary>
    public static IServiceCollection AddEpic2DomainServices(this IServiceCollection services)
    {
        // Domain event dispatching
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        
        // Business rule engine (if needed for dynamic rules)
        services.AddScoped<IBusinessRuleEngine, BusinessRuleEngine>();
        
        // Specification query builder
        services.AddScoped<ISpecificationQueryBuilder, SpecificationQueryBuilder>();
        
        return services;
    }

    /// <summary>
    /// Configures caching for Epic 2 + Epic 5 integration.
    /// </summary>
    public static IServiceCollection AddEpic2Caching(this IServiceCollection services)
    {
        // Add memory cache
        services.AddMemoryCache();
        
        // Add distributed cache (Redis recommended for production)
        services.AddStackExchangeRedisCache(options =>
        {
            // Configuration would come from appsettings
            options.Configuration = "localhost:6379";
        });
        
        // Cache invalidation service
        services.AddScoped<ICacheInvalidationService, CacheInvalidationService>();
        
        return services;
    }

    /// <summary>
    /// Example service registration for a module (Chat in this case).
    /// Shows how to register Epic 2 + Epic 5 integrated components.
    /// </summary>
    public static IServiceCollection AddChatModuleEpic2(this IServiceCollection services)
    {
        // Domain repositories with specification support
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IConversationReadRepository, ConversationReadRepository>();
        
        // Application services
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IMessageService, MessageService>();
        
        // AI integration
        services.AddScoped<IAiClient, OpenAiClient>();
        
        // Validators for this module
        services.AddValidatorsFromAssemblyContaining<ProcessMessageCommandEpic2Validator>();
        
        return services;
    }
}

/// <summary>
/// Example retry behavior configuration for different operation types.
/// Integrates with Epic 5 RetryBehavior and Epic 2 retry markers.
/// </summary>
public static class RetryPolicyConfiguration
{
    public static IServiceCollection AddRetryPolicies(this IServiceCollection services)
    {
        services.AddSingleton<IRetryPolicyProvider>(provider =>
        {
            var policies = new Dictionary<string, RetryPolicy>
            {
                ["StandardRetry"] = new RetryPolicy
                {
                    MaxAttempts = 3,
                    BackoffType = BackoffType.Exponential,
                    BaseDelay = TimeSpan.FromSeconds(1),
                    MaxDelay = TimeSpan.FromSeconds(30)
                },
                ["DatabaseRetry"] = new RetryPolicy
                {
                    MaxAttempts = 5,
                    BackoffType = BackoffType.Linear,
                    BaseDelay = TimeSpan.FromMilliseconds(500),
                    MaxDelay = TimeSpan.FromSeconds(10)
                },
                ["ExternalApiRetry"] = new RetryPolicy
                {
                    MaxAttempts = 3,
                    BackoffType = BackoffType.Exponential,
                    BaseDelay = TimeSpan.FromSeconds(2),
                    MaxDelay = TimeSpan.FromMinutes(1)
                }
            };
            
            return new RetryPolicyProvider(policies);
        });
        
        return services;
    }
}

/// <summary>
/// Performance monitoring configuration for Epic 2 + Epic 5 pipeline.
/// </summary>
public static class PerformanceMonitoringConfiguration
{
    public static IServiceCollection AddEpic2PerformanceMonitoring(this IServiceCollection services)
    {
        // Domain operation metrics
        services.AddSingleton<IDomainMetricsCollector, DomainMetricsCollector>();
        
        // Pipeline performance tracking
        services.AddScoped<IPipelinePerformanceTracker, PipelinePerformanceTracker>();
        
        // Business rule execution metrics
        services.AddScoped<IBusinessRuleMetrics, BusinessRuleMetrics>();
        
        return services;
    }
}

// Supporting interfaces and implementations (simplified for example)

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}

public interface IBusinessRuleEngine
{
    Task<ValidationResult> ValidateAsync<T>(T entity, CancellationToken cancellationToken = default);
}

public interface ISpecificationQueryBuilder
{
    IQueryable<T> Apply<T>(IQueryable<T> query, Specification<T> specification);
}

public interface ICacheInvalidationService
{
    Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);
}

public interface IRetryPolicyProvider
{
    RetryPolicy? GetPolicy(string policyName);
}

public interface IDomainMetricsCollector
{
    void RecordAggregateOperation(string aggregateType, string operation, TimeSpan duration);
    void RecordBusinessRuleEvaluation(string ruleType, bool passed, TimeSpan duration);
    void RecordSpecificationExecution(string specificationType, int resultCount, TimeSpan duration);
}

public interface IPipelinePerformanceTracker
{
    void TrackBehaviorExecution(string behaviorType, TimeSpan duration);
    void TrackHandlerExecution(string handlerType, TimeSpan duration);
}

public interface IBusinessRuleMetrics
{
    Task RecordRuleExecutionAsync(string ruleCode, bool passed, TimeSpan executionTime);
    Task<BusinessRuleExecutionStats> GetRuleStatsAsync(string ruleCode);
}

// Simple implementations (production implementations would be more sophisticated)

public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IMediator _mediator;
    
    public DomainEventDispatcher(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }
    }
}

public class BusinessRuleEngine : IBusinessRuleEngine
{
    public Task<ValidationResult> ValidateAsync<T>(T entity, CancellationToken cancellationToken = default)
    {
        // Implementation would validate entity using registered business rules
        return Task.FromResult(new ValidationResult());
    }
}

public class RetryPolicyProvider : IRetryPolicyProvider
{
    private readonly Dictionary<string, RetryPolicy> _policies;
    
    public RetryPolicyProvider(Dictionary<string, RetryPolicy> policies)
    {
        _policies = policies;
    }
    
    public RetryPolicy? GetPolicy(string policyName)
    {
        return _policies.TryGetValue(policyName, out var policy) ? policy : null;
    }
}

public record RetryPolicy
{
    public int MaxAttempts { get; init; }
    public BackoffType BackoffType { get; init; }
    public TimeSpan BaseDelay { get; init; }
    public TimeSpan MaxDelay { get; init; }
}

public enum BackoffType
{
    Linear,
    Exponential,
    Fixed
}

public record BusinessRuleExecutionStats
{
    public string RuleCode { get; init; } = string.Empty;
    public int TotalExecutions { get; init; }
    public int PassedExecutions { get; init; }
    public int FailedExecutions { get; init; }
    public TimeSpan AverageExecutionTime { get; init; }
    public TimeSpan MaxExecutionTime { get; init; }
}

public class ValidationResult
{
    public bool IsValid { get; init; } = true;
    public List<string> Errors { get; init; } = new();
}