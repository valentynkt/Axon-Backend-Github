using BuildingBlocks.Infrastructure.Persistence.Write;

namespace Axon.Modules.Chat.Infrastructure.Persistence;

using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Application.Persistence;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Infrastructure.Persistence.Write;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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