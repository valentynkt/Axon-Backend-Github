using BuildingBlocks.Infrastructure.Persistence.Write;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.Persistence;

public sealed class ChatWriteDbContext
    : WriteDbContextBase<ChatModule>
{
    public ChatWriteDbContext(
        DbContextOptions options,
        ILogger<ChatWriteDbContext>? logger = null)
        : base(options, logger) { }

    public override string ModuleName => "chat";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Default schema per module
        base.OnModelCreating(modelBuilder);

        // Apply entity configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChatWriteDbContext).Assembly);
    }
}