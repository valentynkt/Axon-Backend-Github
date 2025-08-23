using Axon.Modules.Chat.Application.Common.Models;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Read;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;

public sealed class ChatReadDbContext : ReadDbContextBase<ChatModule>, IChatReadDbContext
{
    public ChatReadDbContext(DbContextOptions<ChatReadDbContext> options, ILogger<ChatReadDbContext>? logger = null) 
        : base(options, logger) { }

    public override string ModuleName => "chat";
    

    protected override void ConfigureReadModelOptimizations(ModelBuilder modelBuilder)
    {
        
    }
}