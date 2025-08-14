using BuildingBlocks.Infrastructure.Persistence.Read;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.Persistence;

public sealed class ChatReadDbContext
    : ReadDbContextBase<ChatModule>
{
    public ChatReadDbContext(
        DbContextOptions options,
        ILogger<ChatReadDbContext>? logger = null)
        : base(options, logger) { }

    public override string ModuleName => "chat";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply projections/read-model configs
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChatReadDbContext).Assembly);
    }
}