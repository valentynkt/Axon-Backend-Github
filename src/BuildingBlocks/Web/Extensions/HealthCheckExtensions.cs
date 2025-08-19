using BuildingBlocks.Infrastructure.Messaging.MassTransit;
using BuildingBlocks.Infrastructure.Observability.HealthChecks;
using BuildingBlocks.Infrastructure.Persistence.Postgres;
using BuildingBlocks.Web.HealthChecks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;

namespace BuildingBlocks.Web.Extensions;

public static class HealthCheckExtensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static IServiceCollection AddCustomHealthCheck(this IServiceCollection services)
    {
        var healthOptions = services.GetOptions<HealthOptions>(nameof(HealthOptions));

        if (healthOptions.Enabled)
        {
            var appOptions = services.GetOptions<AppOptions>(nameof(AppOptions));
            var postgresOptions = services.GetOptions<PostgresOptions>(nameof(PostgresOptions));
            var rabbitMqOptions = services.GetOptions<RabbitMqOptions>(nameof(RabbitMqOptions));

            var healthChecksBuilder = services.AddHealthChecks()
                // Add a default liveness check to ensure app is responsive
                .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"])
                // Add clock system health check
                .AddClockHealthCheck()
                .AddRabbitMQ(
                    serviceProvider =>
                    {
                        var factory = new ConnectionFactory
                        {
                            Uri = new Uri($"amqp://{rabbitMqOptions.UserName}:{rabbitMqOptions.Password}@{rabbitMqOptions.HostName}"),
                        };
                        return factory.CreateConnectionAsync();
                    });

            // MongoDB health check temporarily disabled during PostgreSQL migration
            // if (!string.IsNullOrEmpty(mongoOptions.ConnectionString))
            // {
            //     healthChecksBuilder.AddMongoDb(
            //         clientFactory: _ => new MongoClient(mongoOptions.ConnectionString),
            //         name: "MongoDB-Health",
            //         failureStatus: HealthStatus.Unhealthy,
            //         timeout: TimeSpan.FromSeconds(10));
            // }

            if (!string.IsNullOrEmpty(postgresOptions.ConnectionString))
                healthChecksBuilder.AddNpgSql(postgresOptions.ConnectionString);

            services.AddHealthChecksUI(setup =>
                                       {
                                           setup.SetEvaluationTimeInSeconds(60); // time in seconds between check
                                           setup.AddHealthCheckEndpoint($"Self Check - {appOptions.Name}", HealthEndpointPath);
                                       }).AddInMemoryStorage();
        }

        services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        return services;
    }

    public static WebApplication UseCustomHealthCheck(this WebApplication app)
    {
        var healthOptions = app.Configuration.GetOptions<HealthOptions>(nameof(HealthOptions));

        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks(HealthEndpointPath);
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live"),
            });
        }

        if (healthOptions.Enabled)
            app.MapHealthChecksUI(options => options.UIPath = "/health-ui");

        return app;
    }

    /// <summary>
    /// Adds the Clock health check to the service collection.
    /// </summary>
    /// <param name="builder">The health checks builder</param>
    /// <param name="name">The health check name (defaults to "clock")</param>
    /// <param name="failureStatus">The status to report on failure (defaults to Unhealthy)</param>
    /// <param name="tags">Optional tags for the health check</param>
    /// <returns>The health checks builder for chaining</returns>
    public static IHealthChecksBuilder AddClockHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "clock",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        return builder.AddCheck<ClockHealthCheck>(
            name, 
            failureStatus ?? HealthStatus.Unhealthy, 
            tags ?? []);
    }
}