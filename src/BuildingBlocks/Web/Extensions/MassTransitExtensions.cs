using System.Reflection;
using BuildingBlocks.Application.Events.Publishing;
using BuildingBlocks.Infrastructure.Events;
using BuildingBlocks.Infrastructure.Messaging.MassTransit;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace BuildingBlocks.Web.Extensions;

public static class MassTransitExtensions
{
    public static IServiceCollection AddCustomMassTransit(
        this IServiceCollection services,
        IWebHostEnvironment env,
        TransportType transportType,
        params Assembly[] assembly
    )
    {
        services.AddValidateOptions<RabbitMqOptions>();

        // Swap out the Application NoOp publisher with MassTransit-backed publisher
        services.RemoveAll<IIntegrationEventPublisher>();
        services.AddSingleton<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();

        if (env.IsEnvironment("test"))
        {
            services.AddMassTransitTestHarness(cfg =>
            {
                SetupMasstransitConfigurations(services, cfg, transportType, assembly);
            });
        }
        else
        {
            services.AddMassTransit(cfg =>
            {
                SetupMasstransitConfigurations(services, cfg, transportType, assembly);
            });
        }

        return services;
    }

    private static void SetupMasstransitConfigurations(
        IServiceCollection services,
        IBusRegistrationConfigurator configure,
        TransportType transportType,
        params Assembly[] assembly
    )
    {
        // Optional but nice: clean endpoint names
        configure.SetKebabCaseEndpointNameFormatter();

        configure.AddConsumers(assembly);
        configure.AddSagaStateMachines(assembly);
        configure.AddSagas(assembly);
        configure.AddActivities(assembly);

        switch (transportType)
        {
            case TransportType.RabbitMq:
                configure.UsingRabbitMq((context, bus) =>
                {
                    var configuration = context.GetRequiredService<IConfiguration>();
                    var aspire = configuration.GetConnectionString("rabbitmq");

                    if (!string.IsNullOrEmpty(aspire))
                    {
                        bus.Host(new Uri(aspire));
                    }
                    else
                    {
                        var rabbit = services.GetOptions<RabbitMqOptions>(nameof(RabbitMqOptions));
                        ArgumentNullException.ThrowIfNull(rabbit);

                        bus.Host(rabbit.HostName, rabbit.Port > 0 ? rabbit.Port : 5672, "/", h =>
                        {
                            h.Username(rabbit.UserName ?? "guest");
                            h.Password(rabbit.Password ?? "guest");
                        });
                    }

                    // Optional: consumer-side idempotent publish
                    bus.UseInMemoryOutbox();

                    bus.ConfigureEndpoints(context);
                    bus.UseMessageRetry(AddRetryConfiguration);
                });
                break;

            case TransportType.InMemory:
                configure.UsingInMemory((context, bus) =>
                {
                    // Optional: consumer-side idempotent publish
                    bus.UseInMemoryOutbox();

                    bus.ConfigureEndpoints(context);
                    bus.UseMessageRetry(AddRetryConfiguration);
                });
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(transportType), transportType, null);
        }
    }

    private static void AddRetryConfiguration(IRetryConfigurator r)
    {
        r.Exponential(
                3,
                TimeSpan.FromMilliseconds(200),
                TimeSpan.FromMinutes(120),
                TimeSpan.FromMilliseconds(200))
         .Ignore<ValidationException>();
    }
}
