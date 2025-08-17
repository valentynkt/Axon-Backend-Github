using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Application.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

namespace Axon.Modules.Chat.Application.Abstractions.Persistence;

public interface IChatReadDbContext : IReadDbContext<ChatModule>
{
}