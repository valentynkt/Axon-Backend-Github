using System.Reflection;
using System.Text.RegularExpressions;
using BuildingBlocks.Application.Events.Publishing;
using BuildingBlocks.Infrastructure.Events;
using BuildingBlocks.Infrastructure.Messaging.MassTransit;
using BuildingBlocks.Infrastructure.Messaging.Serialization;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.Messaging;

/// <summary>
/// MassTransit infrastructure registration with EF Outbox support.
/// Provides transactional reliability for integration event publishing.
/// </summary>
public static partial class MassTransitRegistration
{
    [GeneratedRegex(@"[^a-zA-Z0-9\-]")]
    private static partial Regex InvalidCharRegex();
    /// <summary>
    /// Registers MassTransit messaging infrastructure with EF Outbox.
    /// Replaces the NoOp publisher with MassTransit-backed implementation.
    /// 
    /// IMPORTANT: EF Outbox tables must be included in your database migrations:
    /// - OutboxMessage: Stores messages to be published
    /// - OutboxState: Tracks outbox processing state
    /// - InboxState: Prevents duplicate message processing (for consumers)
    /// 
    /// The outbox ensures messages are persisted atomically with business data
    /// and dispatched reliably after transaction commit.
    /// </summary>
    /// <typeparam name="TDbContext">The DbContext type that will host the outbox tables</typeparam>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Optional configuration action</param>
    /// <param name="consumerAssemblies">Assemblies to scan for consumers (optional)</param>
    public static IServiceCollection AddInfrastructureMessaging<TDbContext>(
        this IServiceCollection services,
        Action<MassTransitOptions>? configure = null,
        params Assembly[] consumerAssemblies)
        where TDbContext : DbContext
    {
        // Bind configuration from appsettings
        services.AddOptions<MassTransitOptions>()
            .Configure<IConfiguration>((opt, config) =>
            {
                config.GetSection("MassTransit").Bind(opt);
                configure?.Invoke(opt);
            });

        // Replace NoOp publisher with MassTransit implementation
        services.RemoveAll<IIntegrationEventPublisher>();
        services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();

        // Register MassTransit
        services.AddMassTransit(cfg =>
        {
            // Early check for endpoint naming (needs to be set before transport config)
            var tempProvider = services.BuildServiceProvider();
            var tempOptions = tempProvider.GetService<IOptions<MassTransitOptions>>()?.Value ?? new MassTransitOptions();
            
            // Set endpoint naming convention
            if (!string.IsNullOrEmpty(tempOptions.EndpointPrefix))
            {
                cfg.SetEndpointNameFormatter(new PrefixEndpointNameFormatter(ValidateEndpointPrefix(tempOptions.EndpointPrefix)));
            }
            else
            {
                cfg.SetKebabCaseEndpointNameFormatter();
            }

            // Register consumers if provided
            if (consumerAssemblies.Length > 0)
            {
                cfg.AddConsumers(consumerAssemblies);
                cfg.AddSagaStateMachines(consumerAssemblies);
                cfg.AddSagas(consumerAssemblies);
                cfg.AddActivities(consumerAssemblies);
            }

            // Configure transport - this will resolve options inside
            ConfigureTransport<TDbContext>(cfg, services, consumerAssemblies);
        });

        // Add health checks if enabled
        services.AddHealthChecks()
            .AddCheck<Health.MessagingHealthCheck>("messaging", tags: new[] { "ready", "messaging" });

        return services;
    }

    private static void ConfigureTransport<TDbContext>(
        IBusRegistrationConfigurator cfg,
        IServiceCollection services,
        Assembly[] consumerAssemblies)
        where TDbContext : DbContext
    {
        // Determine transport type early for registration
        // This reads from a temporary options instance just for transport selection
        var tempOptions = new MassTransitOptions();
        var config = services.BuildServiceProvider().GetService<IConfiguration>();
        config?.GetSection("MassTransit").Bind(tempOptions);
        
        switch (tempOptions.Transport)
        {
            case TransportType.RabbitMq:
                ConfigureRabbitMq<TDbContext>(cfg, services, consumerAssemblies);
                break;

            case TransportType.AzureServiceBus:
                ConfigureAzureServiceBus<TDbContext>(cfg, services, consumerAssemblies);
                break;

            case TransportType.InMemory:
                ConfigureInMemory<TDbContext>(cfg, services, consumerAssemblies);
                break;

            default:
                throw new NotSupportedException($"Transport type {tempOptions.Transport} is not supported");
        }
    }

    private static void ConfigureRabbitMq<TDbContext>(
        IBusRegistrationConfigurator cfg,
        IServiceCollection services,
        Assembly[] consumerAssemblies)
        where TDbContext : DbContext
    {
        _ = consumerAssemblies; // Part of consistent API pattern
        // Configure EF Outbox first (before transport)
        cfg.AddEntityFrameworkOutbox<TDbContext>(o =>
        {
            // Determine provider from DbContext options
            o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            
            // Use bus outbox for all sends/publishes
            o.UseBusOutbox();
            
            // Query delay for polling (can be overridden for production)
            o.QueryDelay = TimeSpan.FromSeconds(1);
            
            // Provider-specific tuning
            ConfigureOutboxProvider<TDbContext>(o, services);
        });

        cfg.UsingRabbitMq((context, bus) =>
        {
            // Resolve options from DI at configuration time
            var options = context.GetRequiredService<IOptions<MassTransitOptions>>().Value;
            // Note: Endpoint naming is set at the configurator level, not per-transport
            // The PrefixEndpointNameFormatter is already applied if configured

            // Try Aspire connection string first
            var configuration = context.GetRequiredService<IConfiguration>();
            var aspireConnectionString = configuration.GetConnectionString("rabbitmq");

            if (!string.IsNullOrEmpty(aspireConnectionString))
            {
                bus.Host(new Uri(aspireConnectionString));
            }
            else if (options.RabbitMq != null)
            {
                // Use configured options
                var rabbit = options.RabbitMq;
                bus.Host(rabbit.Host, rabbit.VirtualHost, h =>
                {
                    h.Username(rabbit.Username);
                    h.Password(rabbit.Password);

                    if (rabbit.UseSsl)
                    {
                        h.UseSsl(ssl =>
                        {
                            ssl.Protocol = System.Security.Authentication.SslProtocols.Tls12;
                        });
                    }
                });
            }
            else
            {
                // Default to localhost
                bus.Host("localhost", "/", h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });
            }

            // Configure serialization
            ConfigureSerialization(bus, options.Serialization);

            // Configure retry policy
            bus.UseMessageRetry(r => ConfigureRetry(r, options.Retry));

            // Configure observability
            // Note: OpenTelemetry is automatically integrated in MassTransit 8.x
            // No explicit configuration needed

            // Configure endpoints
            bus.ConfigureEndpoints(context);

            // Configure durability
            if (!options.Durable)
            {
                bus.Durable = false;
                bus.AutoDelete = true;
            }
        });
    }

    private static void ConfigureAzureServiceBus<TDbContext>(
        IBusRegistrationConfigurator cfg,
        IServiceCollection services,
        Assembly[] consumerAssemblies)
        where TDbContext : DbContext
    {
        _ = consumerAssemblies; // Part of consistent API pattern
        // Configure EF Outbox first (before transport)
        cfg.AddEntityFrameworkOutbox<TDbContext>(o =>
        {
            o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            o.UseBusOutbox();
            o.QueryDelay = TimeSpan.FromSeconds(1);
            
            // Provider-specific tuning
            ConfigureOutboxProvider<TDbContext>(o, services);
        });

        cfg.UsingAzureServiceBus((context, bus) =>
        {
            // Resolve options from DI at configuration time
            var options = context.GetRequiredService<IOptions<MassTransitOptions>>().Value;
            var connectionString = options.AzureServiceBusConnectionString
                ?? context.GetRequiredService<IConfiguration>().GetConnectionString("azureservicebus")
                ?? throw new InvalidOperationException("Azure Service Bus connection string not configured");

            bus.Host(connectionString);

            // Configure serialization
            ConfigureSerialization(bus, options.Serialization);

            // Configure retry policy
            bus.UseMessageRetry(r => ConfigureRetry(r, options.Retry));

            // Configure observability
            // Note: OpenTelemetry is automatically integrated in MassTransit 8.x
            // No explicit configuration needed

            // Configure endpoints
            bus.ConfigureEndpoints(context);
        });
    }

    private static void ConfigureInMemory<TDbContext>(
        IBusRegistrationConfigurator cfg,
        IServiceCollection services,
        Assembly[] consumerAssemblies)
        where TDbContext : DbContext
    {
        _ = consumerAssemblies; // Part of consistent API pattern
        // Configure EF Outbox even for in-memory (for consistency)
        cfg.AddEntityFrameworkOutbox<TDbContext>(o =>
        {
            o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            o.UseBusOutbox();
            o.QueryDelay = TimeSpan.FromSeconds(1);
            
            // Provider-specific tuning
            ConfigureOutboxProvider<TDbContext>(o, services);
        });

        cfg.UsingInMemory((context, bus) =>
        {
            // Resolve options from DI at configuration time
            var options = context.GetRequiredService<IOptions<MassTransitOptions>>().Value;

            // Configure serialization
            ConfigureSerialization(bus, options.Serialization);

            // Configure retry policy
            bus.UseMessageRetry(r => ConfigureRetry(r, options.Retry));

            // Configure observability (limited for in-memory)
            // Note: OpenTelemetry is automatically integrated in MassTransit 8.x
            // No explicit configuration needed

            // Configure endpoints
            bus.ConfigureEndpoints(context);
        });
    }

    private static void ConfigureSerialization<T>(T configurator, SerializationOptions options)
        where T : IBusFactoryConfigurator
    {
        // Use System.Text.Json with our custom configuration
        var jsonOptions = SystemTextJsonConfigurator.CreateOptions(options);
        configurator.ConfigureJsonSerializerOptions(opts => jsonOptions);
    }

    private static void ConfigureRetry(IRetryConfigurator retry, RetryOptions options)
    {
        retry.Exponential(
            options.MaxAttempts,
            options.InitialInterval,
            options.MaxInterval,
            options.IntervalIncrement);

        // Don't retry validation exceptions (fully qualified to avoid ambiguity)
        retry.Ignore<System.ComponentModel.DataAnnotations.ValidationException>();
        retry.Ignore<FluentValidation.ValidationException>();
        retry.Ignore<ArgumentException>();
        retry.Ignore<ArgumentNullException>();
        retry.Ignore<InvalidOperationException>();
    }

    /// <summary>
    /// Validates and sanitizes endpoint prefix to ensure valid naming.
    /// </summary>
    private static string ValidateEndpointPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return prefix;

        // Remove invalid characters (keep only letters, digits, hyphens)
        var sanitized = InvalidCharRegex().Replace(prefix, "");
        
        // Ensure it doesn't end with a separator
        sanitized = sanitized.TrimEnd('-', '_');
        
        // Add separator if not present
        if (!sanitized.EndsWith('-') && !sanitized.EndsWith('_'))
            sanitized += "-";

        return sanitized;
    }

    /// <summary>
    /// Configures provider-specific EF Outbox settings.
    /// </summary>
    private static void ConfigureOutboxProvider<TDbContext>(IEntityFrameworkOutboxConfigurator config, IServiceCollection services)
        where TDbContext : DbContext
    {
        // Try to detect the database provider from the DbContext configuration
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        
        try
        {
            var dbContext = scope.ServiceProvider.GetService<TDbContext>();
            if (dbContext != null)
            {
                var database = dbContext.Database;
                var providerName = database.ProviderName;
                
                if (providerName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true ||
                    providerName?.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase) == true)
                {
                    // PostgreSQL specific optimizations
                    config.UseBusOutbox(cfg =>
                    {
                        cfg.MessageDeliveryLimit = 100;
                    });
                    
                    // Use advisory locks for PostgreSQL
                    config.LockStatementProvider = new PostgresLockStatementProvider();
                }
                else if (providerName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true ||
                         providerName?.Contains("Microsoft.EntityFrameworkCore.SqlServer", StringComparison.OrdinalIgnoreCase) == true)
                {
                    // SQL Server specific optimizations
                    config.UseBusOutbox(cfg =>
                    {
                        cfg.MessageDeliveryLimit = 100;
                    });
                    
                    // Use UPDLOCK for SQL Server
                    config.LockStatementProvider = new SqlServerLockStatementProvider();
                }
                // Add other providers as needed
            }
        }
        catch
        {
            // If we can't detect the provider, use defaults
            // This is fine - MassTransit will use sensible defaults
        }
    }

    /// <summary>
    /// Custom endpoint name formatter that adds a prefix.
    /// </summary>
    private class PrefixEndpointNameFormatter : IEndpointNameFormatter
    {
        private readonly string _prefix;
        private readonly IEndpointNameFormatter _inner;

        public PrefixEndpointNameFormatter(string prefix)
        {
            _prefix = prefix;
            _inner = KebabCaseEndpointNameFormatter.Instance;
        }

        public string Separator => _inner.Separator;

        public string Consumer<T>() where T : class, IConsumer
            => $"{_prefix}{_inner.Consumer<T>()}";

        public string Message<T>() where T : class
            => $"{_prefix}{_inner.Message<T>()}";

        public string Saga<T>() where T : class, ISaga
            => $"{_prefix}{_inner.Saga<T>()}";

        public string ExecuteActivity<T, TArguments>()
            where T : class, IExecuteActivity<TArguments>
            where TArguments : class
            => $"{_prefix}{_inner.ExecuteActivity<T, TArguments>()}";

        public string CompensateActivity<T, TLog>()
            where T : class, ICompensateActivity<TLog>
            where TLog : class
            => $"{_prefix}{_inner.CompensateActivity<T, TLog>()}";

        public string TemporaryEndpoint(string tag)
            => $"{_prefix}{_inner.TemporaryEndpoint(tag)}";

        public string SanitizeName(string name)
            => _inner.SanitizeName(name);
    }
}