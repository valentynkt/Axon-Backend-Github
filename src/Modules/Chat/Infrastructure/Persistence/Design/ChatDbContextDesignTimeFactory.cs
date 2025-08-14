using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Design;

// ------------------ Write context factory ------------------
public sealed class ChatWriteDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ChatWriteDbContext>
{
    public ChatWriteDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeHelpers.BuildConfiguration();
        var cs = DesignTimeHelpers.ResolveConnectionString(configuration, preferredName: "ChatWriteDb", moduleName: "Chat");

        var options = new DbContextOptionsBuilder<ChatWriteDbContext>()
            .UseNpgsql(cs, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(ChatWriteDbContext).Assembly.GetName().Name);
                npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
            })
            .Options;

        return new ChatWriteDbContext(options);
    }
}


// ------------------ helpers ------------------
internal static class DesignTimeHelpers
{
    public static IConfiguration BuildConfiguration()
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var basePath = Directory.GetCurrentDirectory();

        return new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{env}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    public static string ResolveConnectionString(IConfiguration config, string preferredName, string moduleName)
    {
        // Try (in order):
        // 1) ConnectionStrings:{preferredName}
        // 2) PostgresOptions:ConnectionStrings:{moduleName}
        // 3) PostgresOptions:ConnectionString
        // 4) ConnectionStrings:DefaultConnection
        // 5) Environment variable ConnectionStrings__{preferredName}
        return
            config.GetConnectionString(preferredName) ??
            config[$"PostgresOptions:ConnectionStrings:{moduleName}"] ??
            config["PostgresOptions:ConnectionString"] ??
            config.GetConnectionString("DefaultConnection") ??
            Environment.GetEnvironmentVariable($"ConnectionStrings__{preferredName}") ??
            throw new InvalidOperationException(
                $"No connection string found for '{preferredName}'. " +
                $"Checked ConnectionStrings, PostgresOptions, and env vars.");
    }
}
