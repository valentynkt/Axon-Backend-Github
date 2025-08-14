// Chat/Application/Persistence/IChatReadDbContext.cs

using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

namespace Axon.Modules.Chat.Application.Persistence;

public interface IChatReadDbContext : IReadDbContext<ChatModule>
{
    // No extra members. Add module-specific helpers later only if needed.
}