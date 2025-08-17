using Axon.Modules.Chat.Application.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

namespace Axon.Modules.Chat.Application.Abstractions.Persistence;

public interface IChatWriteDbContext : IWriteDbContext<ChatModule>
{
    // No extra members. Add module-specific helpers later only if needed.
}