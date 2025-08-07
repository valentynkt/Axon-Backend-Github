using BuildingBlocks.Caching;
using BuildingBlocks.Logging;
using BuildingBlocks.Validation;
using Identity.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Extensions.Infrastructure;

using Configurations;

public static class MediatRExtensions
{
    public static IServiceCollection AddCustomMediatR(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(IdentityRoot).Assembly));
        // Pipeline order: Validation → Authorization → Retry → Transaction → Caching → Observability
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        // Note: Authorization and Retry behaviors not yet implemented
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(EfTxIdentityBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(InvalidateCachingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>)); // Observability at the end

        return services;
    }
}