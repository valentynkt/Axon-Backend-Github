using Axon.Modules.Chat.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.EventHandlers;

/// <summary>
/// Handler for MessageAddedDomainEvent
/// </summary>
public sealed class MessageAddedDomainEventHandler : INotificationHandler<MessageAddedDomainEvent>
{
    private readonly ILogger<MessageAddedDomainEventHandler> _logger;

    public MessageAddedDomainEventHandler(ILogger<MessageAddedDomainEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(MessageAddedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Message {MessageId} added to conversation {ConversationId} with role {Role}. First message: {IsFirstMessage}. Content length: {ContentLength}. Event occurred at: {OccurredAt}",
            domainEvent.MessageId,
            domainEvent.ConversationId,
            domainEvent.Role,
            domainEvent.IsFirstMessage,
            domainEvent.Content.Length,
            domainEvent.OccurredAt);

        // Additional side effects can be added here:
        // - Send notifications
        // - Update search indexes
        // - Trigger workflows
        // - Update analytics/metrics

        return Task.CompletedTask;
    }
}