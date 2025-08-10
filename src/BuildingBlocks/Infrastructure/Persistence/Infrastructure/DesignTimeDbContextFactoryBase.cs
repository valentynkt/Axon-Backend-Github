using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace BuildingBlocks.Infrastructure.Persistence.Infrastructure;

/// <summary>
/// Provider-agnostic design-time factory base for EF Core migrations.
/// Derive and implement <see cref="ConfigureProvider"/> to select provider (e.g., Npgsql, SqlServer).
/// </summary>
public abstract class DesignTimeDbContextFactoryBase<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : DbContext
{
    public TContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var basePath = Directory.GetCurrentDirectory();
        return Create(basePath, environment);
    }

    protected abstract TContext CreateNewInstance(DbContextOptions<TContext> options);

    /// <summary>
    /// Configure provider on the options builder (e.g., options.UseNpgsql(conn) or options.UseSqlServer(conn)).
    /// </summary>
    protected abstract void ConfigureProvider(DbContextOptionsBuilder<TContext> builder, string connectionString);

    private TContext Create(string basePath, string environmentName)
    {
        var config = BuildConfiguration(basePath, environmentName);
        var connStr = ResolveConnectionString(config);

        if (string.IsNullOrWhiteSpace(connStr))
            throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        ConfigureProvider(optionsBuilder, connStr);

        Console.WriteLine($"[DesignTimeDbContextFactory] Provider configured. Environment: {environmentName}");

        return CreateNewInstance(optionsBuilder.Options);
    }

    private static IConfigurationRoot BuildConfiguration(string basePath, string environmentName) =>
        new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

    private static string? ResolveConnectionString(IConfiguration config)
    {
        // Standard pattern (ConnectionStrings:DefaultConnection)
        var fromSection = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fromSection))
            return fromSection;

        // Fallback env var for CI/CD (e.g., ConnectionStrings__DefaultConnection)
        var fromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        return fromEnv;
    }
}
