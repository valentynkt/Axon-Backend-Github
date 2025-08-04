using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Domain;

namespace Axon.Modules.Chat.Domain.Events;

/// <summary>
/// Domain event raised when a conversation is archived
/// </summary>
public sealed record ConversationArchivedDomainEvent(
    ConversationId ConversationId,
    int MessageCount,
    DateTime? CompletedAt,
    DateTime ArchivedAt) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid EventId { get; } = Guid.NewGuid();
    public int EventVersion { get; } = 1;
    public string EventType { get; } = nameof(ConversationArchivedDomainEvent);
}