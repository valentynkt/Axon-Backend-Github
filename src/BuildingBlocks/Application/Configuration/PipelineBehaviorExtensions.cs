using BuildingBlocks.Application.Behaviors;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BuildingBlocks.Application.Configuration;

public static class PipelineBehaviorExtensions
{
    public static IServiceCollection AddPipelineBehaviors(
        this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        // Always: observability outermost
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ObservabilityBehavior<,>));

        // Optional: lightweight logging
        var enableReqLogging =
            config.GetValue<bool?>("Pipeline:EnableRequestLogging")
            ?? env.IsDevelopment(); // default: dev-only

        if (enableReqLogging)
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestLoggingBehavior<,>));

        // Resilience (queries only)
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(QueryRetryBehavior<,>));

        // Validation (Fluent + domain)
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestValidationBehavior<,>));

        // Caching + invalidation
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(QueryCachingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CommandCacheInvalidationBehavior<,>));

        // Transactions (commands) – includes post-commit domain notifications + outbox
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CommandTransactionBehavior<,>));

        // Innermost safety net
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ExceptionHandlingBehavior<,>));

        return services;
    }
}