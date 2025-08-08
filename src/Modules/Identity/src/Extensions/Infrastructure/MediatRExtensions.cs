using BuildingBlocks.Application.Extensions;
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
        
        // Register Identity-specific transaction behavior
        // Note: Pipeline behaviors are centrally registered in API layer
        // This Identity-specific behavior will be used for Identity commands/queries
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(EfTxIdentityBehavior<,>));

        return services;
    }
}