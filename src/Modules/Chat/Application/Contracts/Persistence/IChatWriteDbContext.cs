using Axon.Modules.Chat.Application.Common.Models;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

namespace Axon.Modules.Chat.Application.Contracts.Persistence;

public interface IChatWriteDbContext : IWriteDbContext<ChatModule>
{
    // No extra members. Add module-specific helpers later only if needed.
}