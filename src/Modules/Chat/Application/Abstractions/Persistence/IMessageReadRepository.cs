using Axon.Modules.Chat.Domain.Entities;

namespace Axon.Modules.Chat.Application.Abstractions.Persistence;

/// <summary>
/// Read-only repository for Message entities supporting specification-based queries.
/// Enables efficient querying of messages with complex filters and projections.
/// </summary>
public interface IMessageReadRepository : ISpecificationReadRepository<Message>
{
}