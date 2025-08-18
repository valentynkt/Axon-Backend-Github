// File: BuildingBlocks/Infrastructure/Messaging/MassTransitRegistration.cs
using System.Reflection;
using System.Text.RegularExpressions;
using BuildingBlocks.Application.Events.Publishing;
using BuildingBlocks.Infrastructure.Events;
using BuildingBlocks.Infrastructure.Messaging.MassTransit;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Messaging;

public static partial class MassTransitRegistration
{
    [GeneratedRegex(@"[^a-zA-Z0-9\-]")]
    private static partial Regex InvalidCharRegex();

    /// <summary>
    /// MassTransit + EF Bus Outbox with pluggable transport (RabbitMQ/AzureSB/InMemory).
    /// Consumers/sagas/activities can be auto-discovered from assemblies.
    /// </summary>
    public static IServiceCollection AddInfrastructureMessaging<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<MassTransitOptions>? configure = null,
        params Assembly[] consumerAssemblies)
        where TDbContext : DbContext
    {
        // Bind options once; allow caller overrides
        services.Configure<MassTransitOptions>(configuration.GetSection("MassTransit"));
        if (configure is not null) services.PostConfigure(configure);

        // Replace no-op publisher with the MT publisher
        services.RemoveAll<IIntegrationEventPublisher>();
        services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();

        // Transport choice is read directly from configuration (no provider build)
        var transport = configuration
            .GetSection("MassTransit")
            .GetValue("Transport", TransportType.RabbitMq);

        services.AddMassTransit(x =>
        {
            // Discover consumers/sagas if provided
            if (consumerAssemblies is { Length: > 0 })
            {
                x.AddConsumers(consumerAssemblies);
                x.AddSagaStateMachines(consumerAssemblies);
                x.AddSagas(consumerAssemblies);
                x.AddActivities(consumerAssemblies);
            }

            // EF Bus Outbox (one simple path; provider-agnostic)
            x.AddEntityFrameworkOutbox<TDbContext>(o =>
            {
                o.UseBusOutbox();
                // Default polling is fine; uncomment if you need tuning
                // o.QueryDelay = TimeSpan.FromSeconds(1);
            });

            // Consistent default naming style; we'll apply prefix at ConfigureEndpoints
            x.SetKebabCaseEndpointNameFormatter();

            switch (transport)
            {
                case TransportType.RabbitMq:
                    x.UsingRabbitMq((context, cfg) =>
                    {
                        var opts = context.GetRequiredService<IOptions<MassTransitOptions>>().Value;

                        if (!string.IsNullOrWhiteSpace(opts.RabbitMqConnectionString))
                        {
                            cfg.Host(new Uri(opts.RabbitMqConnectionString));
                        }
                        else
                        {
                            cfg.Host(
                                opts.RabbitMq.Host,
                                (ushort)opts.RabbitMq.Port,  // <-- cast to ushort
                                opts.RabbitMq.VirtualHost,
                                h =>
                                {
                                    h.Username(opts.RabbitMq.Username);
                                    h.Password(opts.RabbitMq.Password);
                                    if (opts.RabbitMq.UseSsl)
                                    {
                                        // Provide the lambda overload; empty is fine if defaults are OK
                                        h.UseSsl(_ => { });
                                    }
                                });
                        }

                        ApplyCommon(cfg, opts);

                        if (!opts.Durable)
                        {
                            cfg.Durable = false;
                            cfg.AutoDelete = true;
                        }

                        cfg.ConfigureEndpoints(context, new PrefixFormatterIfAny(opts.EndpointPrefix));
                    });
                    break;


                case TransportType.AzureServiceBus:
                    x.UsingAzureServiceBus((context, cfg) =>
                    {
                        var opts = context.GetRequiredService<IOptions<MassTransitOptions>>().Value;
                        var conn = opts.AzureServiceBusConnectionString
                                   ?? configuration.GetConnectionString("azureservicebus")
                                   ?? throw new InvalidOperationException(
                                       "Azure Service Bus connection string is not configured.");

                        cfg.Host(conn);

                        ApplyCommon(cfg, opts);
                        cfg.ConfigureEndpoints(context, new PrefixFormatterIfAny(opts.EndpointPrefix));
                    });
                    break;

                case TransportType.InMemory:
                    x.UsingInMemory((context, cfg) =>
                    {
                        var opts = context.GetRequiredService<IOptions<MassTransitOptions>>().Value;
                        ApplyCommon(cfg, opts);
                        cfg.ConfigureEndpoints(context, new PrefixFormatterIfAny(opts.EndpointPrefix));
                    });
                    break;

                default:
                    throw new NotSupportedException($"Unsupported transport: {transport}");
            }
        });

        // MT hosted service is added automatically.
        return services;
    }

    private static void ApplyCommon(IBusFactoryConfigurator cfg, MassTransitOptions opts)
    {
        cfg.UseMessageRetry(r => r.Exponential(
            retryLimit: opts.Retry.MaxAttempts,
            minInterval: opts.Retry.InitialInterval,
            maxInterval: opts.Retry.MaxInterval,
            intervalDelta: opts.Retry.IntervalIncrement));

        // Leave serialization & OpenTelemetry to MassTransit defaults
        // (System.Text.Json + W3C TraceContext + OTel activities)
    }

    /// <summary>Prefix wrapper that defers to kebab-case formatter.</summary>
    private sealed class PrefixFormatterIfAny : IEndpointNameFormatter
    {
        private readonly string? _prefix;
        private readonly IEndpointNameFormatter _inner = KebabCaseEndpointNameFormatter.Instance;

        public PrefixFormatterIfAny(string? prefix) => _prefix = Sanitize(prefix);

        public string Separator => _inner.Separator;

        public string Consumer<T>() where T : class, IConsumer => Prefix(_inner.Consumer<T>());
        public string Message<T>() where T : class => Prefix(_inner.Message<T>());
        public string Saga<T>() where T : class, ISaga => Prefix(_inner.Saga<T>());
        public string ExecuteActivity<T, TArgs>() where T : class, IExecuteActivity<TArgs> where TArgs : class
            => Prefix(_inner.ExecuteActivity<T, TArgs>());
        public string CompensateActivity<T, TLog>() where T : class, ICompensateActivity<TLog> where TLog : class
            => Prefix(_inner.CompensateActivity<T, TLog>());
        public string TemporaryEndpoint(string tag) => Prefix(_inner.TemporaryEndpoint(tag));
        public string SanitizeName(string name) => _inner.SanitizeName(name);

        private string Prefix(string name) => string.IsNullOrEmpty(_prefix) ? name : $"{_prefix}{name}";

        private static string? Sanitize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var clean = InvalidCharRegex().Replace(s, string.Empty).TrimEnd('-', '_');
            return clean.Length == 0 ? null : (clean.EndsWith('-') || clean.EndsWith('_') ? clean : clean + "-");
        }
    }
}
