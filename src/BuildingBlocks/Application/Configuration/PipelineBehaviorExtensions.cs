using BuildingBlocks.Application.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Application.Configuration;

/// <summary>
/// Extension methods for registering MediatR pipeline behaviors in the correct order.
/// 
/// CRITICAL: The registration order is absolutely critical as it defines the execution
/// pipeline from outermost to innermost behavior. This order matches Epic 5 specification
/// and must not be changed without careful consideration.
/// </summary>
public static class PipelineBehaviorExtensions
{
    /// <summary>
    /// Registers all MediatR pipeline behaviors in the correct order as specified in Epic 5.
    /// 
    /// Execution Order (outermost to innermost):
    /// 1. ObservabilityBehavior - Activity tracing, metrics, and telemetry
    /// 2. LoggingBehavior - Structured logging with correlation IDs  
    /// 3. RetryBehavior - Transient failure recovery with exponential backoff
    /// 4. ValidationBehavior - Request validation with error aggregation
    /// 5. CachingBehavior - Query result caching with intelligent key generation
    /// 6. TransactionBehavior - Database transaction management with outbox pattern
    /// </summary>
    /// <param name="services">The service collection to register behaviors with</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddPipelineBehaviors(this IServiceCollection services)
    {
        // IMPORTANT: The registration order here is critical as it defines the
        // execution pipeline for MediatR behaviors, from outermost to innermost.

        // 1. (Outermost) Observability and general error handling. This wraps the
        // entire operation to ensure all exceptions are caught and all telemetry is captured.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ObservabilityBehavior<,>));
        
        // 2. Structured logging with correlation IDs and request/response data
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestLoggingBehavior<,>));

        // 3. Resilience. This wraps the core logic to allow for retries of the
        // entire unit of work on transient failures.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(QueryRetryBehavior<,>));
        
        // 4a. FluentValidation - Request validation with error aggregation
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestValidationBehavior<,>));
        
        // 4b. Domain validation - Business rule validation for commands
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DomainValidationBehavior<,>));
        
        // 5. Caching. This is for queries. If a result is found in the cache,
        // subsequent behaviors (like Transaction) will be skipped.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(QueryCachingBehavior<,>));
        
        // Additional caching behavior for cache invalidation on commands
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CommandCacheInvalidationBehavior<,>));

        // 6. (Innermost) Transaction Management. This ensures that the actual
        // command handler logic runs within a database transaction.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CommandTransactionBehavior<,>));

        return services;
    }
}