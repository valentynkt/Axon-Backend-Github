using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Infrastructure.Persistence.Infrastructure;
using Axon.Modules.Chat.Application.Common.Models;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using BuildingBlocks.Core.Domain.Entities.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;

public sealed class ChatDbContext : WriteDbContextBase<ChatModule>, IChatWriteDbContext
{
    private readonly TimeProvider _timeProvider;

    public ChatDbContext(
        DbContextOptions<ChatDbContext> options,
        TimeProvider timeProvider,
        ILogger<ChatDbContext>? logger = null)
        : base(options, logger)
    {
        _timeProvider = timeProvider;
    }

    public override string ModuleName => "chat";

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Base class already calls HasDefaultSchema(ModuleName.ToLowerInvariant())
        // No need to duplicate schema configuration
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChatDbContext).Assembly);

        // Explicitly apply configurations to ensure they work in test contexts
        // The assembly scan might not work correctly in some test scenarios
        var conversationConfig = new Persistence.Configurations.ConversationConfiguration();
        conversationConfig.Configure(modelBuilder.Entity<Conversation>());

        var messageConfig = new Persistence.Configurations.MessageConfiguration();
        messageConfig.Configure(modelBuilder.Entity<Message>());
    }

    protected override void ApplyAuditInformation()
    {
        var now = _timeProvider.GetUtcNow();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is IAuditable auditableEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    // For new entities, set both CreatedAt and UpdatedAt using reflection
                    // since we can't cast to the generic type without knowing TId
                    SetAuditTimestamp(auditableEntity, "SetCreatedAtInternal", now);
                    SetAuditTimestamp(auditableEntity, "SetUpdatedAtInternal", now);
                }
                else if (entry.State == EntityState.Modified)
                {
                    // For modified entities, only update UpdatedAt
                    SetAuditTimestamp(auditableEntity, "SetUpdatedAtInternal", now);
                }
            }
        }
    }

    private static void SetAuditTimestamp(IAuditable entity, string methodName, DateTimeOffset timestamp)
    {
        var method = entity.GetType().GetMethod(methodName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(entity, new object[] { timestamp });
    }
}

/// <summary>
/// Design-time factory for ChatDbContext to support EF Core tools (migrations, etc.)
/// </summary>
public sealed class ChatDbContextFactory : DesignTimeDbContextFactoryBase<ChatDbContext>
{
    protected override ChatDbContext CreateNewInstance(DbContextOptions<ChatDbContext> options) =>
        new(options, TimeProvider.System);

    protected override void ConfigureProvider(DbContextOptionsBuilder<ChatDbContext> builder, string connectionString) =>
        builder.UseNpgsql(connectionString, opt =>
        {
            opt.MigrationsAssembly(typeof(ChatDbContext).Assembly.FullName);
            opt.MigrationsHistoryTable("__EFMigrationsHistory", "chat");
        });
}