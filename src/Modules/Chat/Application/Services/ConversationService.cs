using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Application.Repositories;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Conversation;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Modules.Chat.Domain.ValueObjects;
 
using BuildingBlocks.Core.Functional.Results;
using Microsoft.Extensions.Logging;
using AiRequest = Axon.Modules.Chat.Application.DTOs.AiRequest;
using IAiClient = Axon.Modules.Chat.Application.Abstractions.IAiClient;

namespace Axon.Modules.Chat.Application.Services;

/// <summary>
/// Application service for complex conversation workflows
/// </summary>
public interface IConversationService
{
    /// <summary>
    /// Creates a conversation and processes the initial message with AI
    /// </summary>
    Task<Result<ConversationDto>> StartConversationWithMessageAsync(
        string title, 
        string initialMessage, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a message with AI and adds both user and assistant messages
    /// </summary>
    Task<Result<MessageDto>> ProcessMessageAsync(
        ConversationId conversationId,
        string userMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives old completed conversations
    /// </summary>
    Task<Result<int>> ArchiveOldConversationsAsync(
        TimeSpan olderThan,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of conversation service
/// </summary>
public sealed class ConversationService : IConversationService
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAiClient _aiClient;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(
        IConversationRepository conversationRepository,
        IUnitOfWork unitOfWork,
        IAiClient aiClient,
        ILogger<ConversationService> logger)
    {
        _conversationRepository = conversationRepository;
        _unitOfWork = unitOfWork;
        _aiClient = aiClient;
        _logger = logger;
    }

    public async Task<Result<ConversationDto>> StartConversationWithMessageAsync(
        string title,
        string initialMessage,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting new conversation with title: {Title}", title);

        // Create conversation
        var conversationResult = Conversation.Create(title, "default-user");
        if (conversationResult.IsFailure)
            return conversationResult.Error;

        var conversation = conversationResult.Value;

        // Add initial user message
        var userMessageResult = conversation.AddMessage(initialMessage, MessageRole.User);
        if (userMessageResult.IsFailure)
            return userMessageResult.Error;

        // Process message with AI
        var aiRequest = new AiRequest(
            Message: initialMessage,
            McpConfigs: null,
            PreviousResponseId: null);

        var aiResponse = await _aiClient.ProcessMessageAsync(aiRequest, cancellationToken);
        if (aiResponse.IsFailure)
        {
            _logger.LogWarning("AI processing failed for conversation {ConversationId}: {Error}",
                conversation.Id, aiResponse.Error);
            return aiResponse.Error;
        }

        // Add assistant response
        var assistantMessageResult = conversation.AddMessage(
            aiResponse.Value.Content, 
            MessageRole.Assistant);
        if (assistantMessageResult.IsFailure)
            return assistantMessageResult.Error;

        // Save conversation
        await _conversationRepository.AddAsync(conversation, cancellationToken);
        
        var saveResult = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (saveResult == 0)
            return Error.Persistence("Failed to save conversation");

        // Map to DTO using audit accessor methods
        var messagesDto = conversation.MessagesOrdered
            .Select(m => new MessageDto(
                m.Id,
                m.ConversationId,
                m.Content,
                m.Role.Value,
                m.Sequence,
                m.GetCreatedAt(),
                m.Metadata))
            .ToList();

        var conversationDto = new ConversationDto(
            conversation.Id,
            conversation.Title,
            conversation.Status.ToString(),
            conversation.GetCreatedAt(),
            conversation.CompletedAt,
            conversation.MessageCount,
            messagesDto);

        _logger.LogInformation("Successfully started conversation {ConversationId} with {MessageCount} messages",
            conversation.Id, conversation.MessageCount);

        return conversationDto;
    }

    public async Task<Result<MessageDto>> ProcessMessageAsync(
        ConversationId conversationId,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing message for conversation {ConversationId}", conversationId);

        // Get conversation
        var conversationResult = await _conversationRepository.GetAggregateAsync(conversationId, cancellationToken);
        if (conversationResult.IsFailure)
            return conversationResult.Error;
            
        var conversation = conversationResult.Value;
        if (conversation is null)
            return Error.NotFound($"Conversation with ID {conversationId} not found");

        // Add user message
        var userMessageResult = conversation.AddMessage(userMessage, MessageRole.User);
        if (userMessageResult.IsFailure)
            return userMessageResult.Error;

        // Build conversation context for AI using domain method
        var conversationContext = conversation.BuildConversationContext();

        // Process with AI
        var aiRequest = new AiRequest(
            Message: conversationContext,
            McpConfigs: null,
            PreviousResponseId: null);

        var aiResponse = await _aiClient.ProcessMessageAsync(aiRequest, cancellationToken);
        if (aiResponse.IsFailure)
        {
            _logger.LogWarning("AI processing failed for conversation {ConversationId}: {Error}",
                conversationId, aiResponse.Error);
            return aiResponse.Error;
        }

        // Add assistant response
        var assistantMessageResult = conversation.AddMessage(
            aiResponse.Value.Content, 
            MessageRole.Assistant);
        if (assistantMessageResult.IsFailure)
            return assistantMessageResult.Error;

        var assistantMessage = assistantMessageResult.Value;

        // Update conversation
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        
        var saveResult = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (saveResult == 0)
            return Error.Persistence("Failed to save conversation");

        // Return assistant message DTO using audit accessor methods
        var messageDto = new MessageDto(
            assistantMessage.Id,
            assistantMessage.ConversationId,
            assistantMessage.Content,
            assistantMessage.Role.Value,
            assistantMessage.Sequence,
            assistantMessage.GetCreatedAt(),
            assistantMessage.Metadata);

        _logger.LogInformation("Successfully processed message for conversation {ConversationId}, assistant message {MessageId}",
            conversationId, assistantMessage.Id);

        return messageDto;
    }

    public async Task<Result<int>> ArchiveOldConversationsAsync(
        TimeSpan olderThan,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Archiving conversations older than {TimeSpan}", olderThan);

        var cutoffDate = DateTime.UtcNow - olderThan;
        var completedConversationsResult = await _conversationRepository.GetByStatusAsync(
            ConversationStatus.Completed, 
            0, 
            1000, // Process in batches
            cancellationToken);

        if (completedConversationsResult.IsFailure)
            return completedConversationsResult.Error;

        var conversationsToArchive = completedConversationsResult.Value.Items
            .Where(c => c.CompletedAt.HasValue && c.CompletedAt.Value < cutoffDate)
            .ToList();

        if (!conversationsToArchive.Any())
        {
            _logger.LogInformation("No conversations found to archive");
            return 0;
        }

        var archivedCount = 0;
        foreach (var conversation in conversationsToArchive)
        {
            // Archive conversation using domain method
            var archiveResult = conversation.ArchiveConversation();
            if (archiveResult.IsFailure)
            {
                _logger.LogWarning("Failed to archive conversation {ConversationId}: {Error}",
                    conversation.Id, archiveResult.Error);
                continue;
            }

            await _conversationRepository.UpdateAsync(conversation, cancellationToken);
            _logger.LogInformation("Archived conversation {ConversationId} completed at {CompletedAt}",
                conversation.Id, conversation.CompletedAt);
            archivedCount++;
        }

        // Save all archived conversations
        if (archivedCount > 0)
        {
            var saveResult = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saveResult == 0)
            {
                _logger.LogError("Failed to save archived conversations");
                return Error.Persistence("Failed to save archived conversations");
            }
        }

        _logger.LogInformation("Archived {Count} old conversations", archivedCount);
        return archivedCount;
    }
}