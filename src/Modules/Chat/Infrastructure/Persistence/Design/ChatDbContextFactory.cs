using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Design;

/// <summary>
/// Design-time factory for ChatDbContext to support EF Core migrations
/// Creates a minimal context without complex dependencies for migrations
/// </summary>
public sealed class ChatDbContextFactory : IDesignTimeDbContextFactory<MinimalChatDbContext>
{
    public MinimalChatDbContext CreateDbContext(string[] args)
    {
        // Create DbContextOptions for design-time
        var optionsBuilder = new DbContextOptionsBuilder<MinimalChatDbContext>();
        
        // Use a default connection string for migrations
        var connectionString = GetConnectionString(args);
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly("Axon.Modules.Chat.Infrastructure");
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
        });

        return new MinimalChatDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// Gets connection string from command line arguments or environment variables
    /// Falls back to a default PostgreSQL connection string for local development
    /// </summary>
    private static string GetConnectionString(string[] args)
    {
        // Check command line arguments for connection string
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--connection-string", StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        // Check environment variable
        var envConnectionString = Environment.GetEnvironmentVariable("AXON_CONNECTION_STRING");
        if (!string.IsNullOrEmpty(envConnectionString))
        {
            return envConnectionString;
        }

        // Default connection string for local development
        return "Host=localhost;Database=axon_chat;Username=postgres;Password=postgres;Port=5432";
    }

}