using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;

/// <summary>
/// Design-time factory for IdentityDbContext.
/// Used by EF Core tooling for migrations and schema generation.
/// </summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        
        // Use a default connection string for migrations
        // This will be overridden at runtime by the actual configuration
        var connectionString = GetConnectionString(args);
        
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
        })
        .UseSnakeCaseNamingConvention();

        // Create a simple console logger for design-time operations
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<IdentityDbContext>();
        
        return new IdentityDbContext(optionsBuilder.Options, logger);
    }

    private static string GetConnectionString(string[] args)
    {
        // Look for connection string in args first
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--connection", StringComparison.OrdinalIgnoreCase) ||
                args[i].Equals("-c", StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        // Default connection string for development
        return "Host=localhost;Database=axon_identity;Username=postgres;Password=postgres;Port=5432";
    }
}