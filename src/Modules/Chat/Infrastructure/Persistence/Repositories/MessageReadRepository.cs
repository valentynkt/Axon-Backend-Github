using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Infrastructure.Persistence.Read;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read-only repository implementation for message queries using Entity Framework Core.
/// Supports specification-based queries through Ardalis.Specification integration.
/// </summary>
internal sealed class MessageReadRepository : EfSpecificationReadRepository<Message>, IMessageReadRepository
{
    public MessageReadRepository(ChatReadDbContext dbContext) : base(dbContext)
    {
    }
}