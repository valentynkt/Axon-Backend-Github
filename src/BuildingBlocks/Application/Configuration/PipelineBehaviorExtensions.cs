using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Core.Idempotency;
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
        services.AddCachingServices();
        
        // Always: observability outermost
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ObservabilityBehavior_Result<,>));

        // Optional: lightweight logging
        var enableReqLogging =
            config.GetValue<bool?>("Pipeline:EnableRequestLogging")
            ?? env.IsDevelopment(); // default: dev-only

        if (enableReqLogging)
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestLoggingBehavior<,>));

        // Resilience (queries only)
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(QueryRetryBehavior<,>));

     // Startup/Composition root
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestValidationBehavior_Result<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestValidationBehavior_Unit<>));


        // Idempotency (commands only)
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));

        // Caching + invalidation
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(QueryCachingBehavior<,>));
        

        // Innermost safety net
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ExceptionHandlingBehavior<,>));


        return services;
    }
}