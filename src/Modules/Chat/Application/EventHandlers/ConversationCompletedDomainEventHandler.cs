using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.EventHandlers;

/// <summary>
/// Handler for ConversationCompletedDomainEvent
/// </summary>
public sealed class ConversationCompletedDomainEventHandler : INotificationHandler<ConversationCompletedDomainEvent>
{
    private readonly ILogger<ConversationCompletedDomainEventHandler> _logger;

    public ConversationCompletedDomainEventHandler(ILogger<ConversationCompletedDomainEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(ConversationCompletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Conversation {ConversationId} completed with {MessageCount} messages and {ToolExecutionCount} tool executions. Reason: {CompletionReason}. Completed at: {CompletedAt}. Event occurred at: {OccurredAt}",
            domainEvent.ConversationId,
            domainEvent.MessageCount,
            domainEvent.ToolExecutionCount,
            domainEvent.CompletionReason,
            domainEvent.CompletedAt,
            domainEvent.OccurredAt);

        // Additional side effects can be added here:
        // - Archive conversation data
        // - Generate summary reports
        // - Send completion notifications
        // - Update conversation statistics
        // - Trigger cleanup workflows

        return Task.CompletedTask;
    }
}