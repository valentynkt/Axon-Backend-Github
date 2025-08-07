using Ardalis.GuardClauses;
using BuildingBlocks.Web;
using Humanizer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.PersistMessageProcessor;

public static class Extensions
{
    public static IServiceCollection AddPersistMessageProcessor(this WebApplicationBuilder builder, string? connectionName = "persist-message")
    {
        Guard.Against.Null(builder, nameof(builder));
        
        // Database provider configuration removed

        builder.Services.AddValidateOptions<PersistMessageOptions>();

        builder.Services.AddDbContext<PersistMessageDbContext>(
            (sp, options) =>
            {
                var aspireConnectionString = builder.Configuration.GetConnectionString(connectionName?.Kebaberize() ?? "persist-message");
                var persistMessageOptions = sp.GetService<PersistMessageOptions>();
                
                var connectionString = aspireConnectionString ?? persistMessageOptions?.ConnectionString;

                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new ArgumentException(
                        $"Connection string not found. Please provide either a connection string named '{connectionName?.Kebaberize()}' " +
                        "in configuration or set PersistMessageOptions.ConnectionString");
                }

                // Database provider configuration to be implemented
                // options.UseInMemoryDatabase("PersistMessages");

                // Todo: follow up the issues of .net 9 to use better approach that will provided by .net!
                options.ConfigureWarnings(
                    w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
            });

        builder.Services.AddScoped<IPersistMessageDbContext>(
            provider =>
            {
                var persistMessageDbContext =
                    provider.GetRequiredService<PersistMessageDbContext>();

                persistMessageDbContext.Database.EnsureCreated();
                persistMessageDbContext.CreatePersistMessageTableIfNotExists();

                return persistMessageDbContext;
            });

        builder.Services.AddScoped<IPersistMessageProcessor, PersistMessageProcessor>();

        builder.Services.AddHostedService<PersistMessageBackgroundService>();

        return builder.Services;
    }
}