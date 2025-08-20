// /BuildingBlocks/Application/Validation/ValidationServiceCollectionExtensions.cs
#nullable enable
using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using BuildingBlocks.Application.Validation.Behaviors;
using CSharpFunctionalExtensions;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Application.Validation;

public static class ValidationServiceCollectionExtensions
{
    /// <summary>
    /// Registers FluentValidation validators and MediatR validation behaviors
    /// for Result&lt;T, Error&gt; and UnitResult&lt;Error&gt; pipelines.
    /// </summary>
    public static IServiceCollection AddApplicationValidation(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies is { Length: > 0 })
            services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);

        // Pipeline behaviors (order matters: validation should run early)
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));   // Result<T, Error>
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationUnitBehavior<>)); // UnitResult<Error>

        return services;
    }
}