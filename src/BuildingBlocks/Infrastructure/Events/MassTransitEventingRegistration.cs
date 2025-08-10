using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using BuildingBlocks.Application.Events.Publishing;

namespace BuildingBlocks.Infrastructure.Events;

/// <summary>
/// MassTransit + EF Outbox registration helpers.
/// Keep Infra minimal; transports/config are provided by caller.
/// </summary>
public static class MassTransitEventingRegistration
{
    /// <summary>
    /// Registers MassTransit with EF Outbox and the IntegrationEvent publisher.
    /// You supply bus/transport configuration via <paramref name="configureBus"/>.
    /// </summary>
    /// <typeparam name="TDbContext">Your application's EF DbContext used for transactions</typeparam>
    /// <param name="services">DI</param>
    /// <param name="configureBus">
    /// Configure MassTransit (transport, endpoints, consumers, etc).
    /// Example:
    /// <code>
    /// services.AddMassTransitEventing<AppDbContext>((ctx, cfg) =>
    /// {
    ///     cfg.UsingRabbitMq((context, bus) =>
    ///     {
    ///         bus.Host("rabbitmq", "/", h => { h.Username("guest"); h.Password("guest"); });
    ///         bus.ConfigureEndpoints(context);
    ///     });
    /// });
    /// </code>
    /// </param>
    public static IServiceCollection AddMassTransitEventing<TDbContext>(
        this IServiceCollection services,
        Action<IBusRegistrationContext, IBusRegistrationConfigurator> configureBus)
        where TDbContext : DbContext
    {
        services.AddMassTransit((context, cfg) =>
        {
            // Nice endpoint names
            cfg.SetKebabCaseEndpointNameFormatter();

            // EF Outbox: captures Publish/Send inside the same ambient EF transaction
            cfg.AddEntityFrameworkOutbox<TDbContext>(o =>
            {
                // defaults are fine; tweak if needed
                o.QueryDelay = TimeSpan.FromSeconds(1);
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(1);
                o.UseBusOutbox(); // recommended
            });

            // Let caller pick transport/topology/consumers.
            configureBus(context, cfg);
        });

        // Publisher port implementation
        services.TryAddSingleton<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();

        return services;
    }
}
