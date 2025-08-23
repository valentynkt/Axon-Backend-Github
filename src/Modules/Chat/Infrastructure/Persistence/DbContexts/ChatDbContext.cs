using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Infrastructure.Persistence.Infrastructure;
using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Application.Common.Models;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Application.Persistence;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;

public sealed class ChatDbContext : WriteDbContextBase<ChatModule>, IChatWriteDbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options, ILogger<ChatDbContext>? logger = null) 
        : base(options, logger)
    {
    }

    public override string ModuleName => "chat";

    public DbSet<Conversation> Conversations => Set<Conversation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Base class already calls HasDefaultSchema(ModuleName.ToLowerInvariant())
        // No need to duplicate schema configuration
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChatDbContext).Assembly);
    }
}

/// <summary>
/// Design-time factory for ChatDbContext to support EF Core tools (migrations, etc.)
/// </summary>
public sealed class ChatDbContextFactory : DesignTimeDbContextFactoryBase<ChatDbContext>
{
    protected override ChatDbContext CreateNewInstance(DbContextOptions<ChatDbContext> options) =>
        new(options);

    protected override void ConfigureProvider(DbContextOptionsBuilder<ChatDbContext> builder, string connectionString) =>
        builder.UseNpgsql(connectionString, opt => opt.MigrationsAssembly(typeof(ChatDbContext).Assembly.FullName))
               .UseSnakeCaseNamingConvention();
}