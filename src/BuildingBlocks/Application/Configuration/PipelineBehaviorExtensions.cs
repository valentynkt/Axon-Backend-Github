// /BuildingBlocks/Application/Configuration/PipelineBehaviorExtensions.cs
#nullable enable
using System;
using System.Reflection;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Core.Abstractions.Idempotency;
using BuildingBlocks.Core.Idempotency;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Application.Configuration;

/// <summary>
/// Registers all Application pipeline behaviors in the correct execution order
/// (outer → inner) and wires up FluentValidation validators.
/// 
/// Execution order:
/// 1) ObservabilityBehavior        – tracing/metrics/logging
/// 2) AuthenticationBehavior       – user authentication (IAuthenticatedRequest only)
/// 3) PaginationBehavior           – pagination parameter validation (IPaginatedRequest only)
/// 4) RequestValidationBehavior    – FluentValidation (fail-fast)
/// 5) QueryCachingBehavior         – L1/L2 read-through cache (queries only)
/// 6) QueryRetryBehavior           – Polly-native retry (queries w/ [Retryable])
/// 7) IdempotencyBehavior          – success-only cache for commands
/// 8) UnitOfWorkBehavior           – transactional commit on success (commands)
/// 9) Handler
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

        // 2) Register IdempotencyBehavior dependencies
        services.Configure<IdempotencyOptions>(options => { }); // Use default configuration
        services.AddHttpContextAccessor(); // For HTTP header-based idempotency keys
        services.AddSingleton<IdempotencyKeyResolver>();

        // 3) MediatR pipeline (outer → inner)
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ObservabilityBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthenticationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PaginationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(QueryCachingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(QueryRetryBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

        return services;
    }
}
