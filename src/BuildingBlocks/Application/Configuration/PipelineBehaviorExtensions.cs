// /BuildingBlocks/Application/Behaviors/PipelineBehaviorExtensions.cs
#nullable enable
using System;
using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Registers all Application pipeline behaviors in the correct execution order
/// (outer → inner) and wires up FluentValidation validators.
/// 
/// Execution order:
/// 1) ObservabilityBehavior        – tracing/metrics/logging
/// 2) RequestValidationBehavior    – FluentValidation (fail-fast)
/// 3) QueryCachingBehavior         – L1/L2 read-through cache (queries only)
/// 4) QueryRetryBehavior           – Polly-native retry (queries w/ [Retryable])
/// 5) IdempotencyBehavior          – success-only cache for commands
/// 6) UnitOfWorkBehavior           – transactional commit on success (commands)
/// 7) Handler
/// 
/// Notes:
/// - Registration order matters in MediatR; earlier is outermost.
/// - Validators: pass specific assemblies to limit scanning, or omit to scan all loaded assemblies.
/// </summary>
public static class PipelineBehaviorExtensions
{
    /// <summary>
    /// Add Axon pipeline behaviors and register FluentValidation validators.
    /// </summary>
    /// <param name="services">DI container</param>
    /// <param name="validatorAssemblies">
    /// Optional assemblies to scan for FluentValidation validators.
    /// If empty, all currently loaded assemblies are scanned.
    /// </param>
    public static IServiceCollection AddApplicationPipelineBehaviors(
        this IServiceCollection services,
        params Assembly[] validatorAssemblies)
    {
        // 1) Validators (FluentValidation)
        var assembliesToScan = (validatorAssemblies is { Length: > 0 })
            ? validatorAssemblies
            : AppDomain.CurrentDomain.GetAssemblies();

        services.AddValidatorsFromAssemblies(assembliesToScan, includeInternalTypes: true);

        // 2) MediatR pipeline (outer → inner)
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ObservabilityBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(QueryCachingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(QueryRetryBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

        return services;
    }
}
