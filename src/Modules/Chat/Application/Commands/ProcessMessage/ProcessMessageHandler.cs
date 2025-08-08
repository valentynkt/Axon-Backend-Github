using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Application.Repositories;
using Axon.Modules.Chat.Application.Services;
using Axon.Modules.Chat.Domain.Conversation;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Shared.Common;
using BuildingBlocks.Core.Functional.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Commands.ProcessMessage;

/// <summary>
/// Coordinator for processing chat messages with full conversation management
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Structured logging with interpolation is more readable")]
public sealed class ProcessMessageHandler : IRequestHandler<ProcessMessageCommand, Result<ProcessMessageResponse>>
{
    private readonly IAiClient _aiClient;
    private readonly IMessageRequestBuilder _requestBuilder;
    private readonly IResponseMappingService _responseMappingService;
    private readonly IConversationRepository _conversationRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessMessageHandler> _logger;

    public ProcessMessageHandler(
        IAiClient aiClient,
        IMessageRequestBuilder requestBuilder,
        IResponseMappingService responseMappingService,
        IConversationRepository conversationRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        ILogger<ProcessMessageHandler> logger)
    {
        _aiClient = aiClient;
        _requestBuilder = requestBuilder;
        _responseMappingService = responseMappingService;
        _conversationRepository = conversationRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ProcessMessageResponse>> Handle(
        ProcessMessageCommand request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return Error.Validation("ProcessMessageCommand cannot be null");
        
        _logger.LogInformation(
            "Processing message with length {MessageLength}, ConversationId: {ConversationId}",
            request.Message.Length, request.ConversationId);

        // Get current user ID or use system default
        var userId = request.UserId ?? _currentUserService.GetCurrentUserIdOrSystem();

        // Load or create conversation
        var conversationResult = await GetOrCreateConversationAsync(request.ConversationId, userId, cancellationToken);
        if (conversationResult.IsFailure)
            return conversationResult.Error;

        var conversation = conversationResult.Value;

        // Add user message to conversation
        var userMessageResult = conversation.AddMessage(request.Message, MessageRole.User);
        if (userMessageResult.IsFailure)
            return userMessageResult.Error;

        // Build AI request with conversation context
        var aiRequestResult = _requestBuilder.BuildAiRequest(request, conversation.BuildConversationContext());
        if (aiRequestResult.IsFailure)
            return aiRequestResult.Error;

        var (aiRequest, mcpServerCount) = aiRequestResult.Value;

        // Process message via AI client
        var processResult = await _aiClient.ProcessMessageAsync(aiRequest, cancellationToken);
        if (processResult.IsFailure)
        {
            _logger.LogError(
                "AI client failed to process message: {Error}",
                processResult.Error);
            return processResult.Error;
        }

        var aiResponse = processResult.Value;
        
        // Add AI response to conversation
        var assistantMessageResult = conversation.AddMessage(aiResponse.Content, MessageRole.Assistant);
        if (assistantMessageResult.IsFailure)
            return assistantMessageResult.Error;

        // Save conversation changes
        // NOTE: No UpdateAsync call needed - EF Core automatically tracks changes for loaded entities
        // For new conversations, AddAsync already marked them for INSERT
        // For existing conversations, GetAggregateAsync already loaded them with change tracking
        
        var commitResult = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (commitResult == 0)
            return Error.Persistence("Failed to save changes");

        // Map AI response to API response
        var response = _responseMappingService.MapToApiResponse(aiResponse);
        
        // Create enriched response with conversation context
        var enrichedResponse = new ProcessMessageResponse(
            response.Response,
            conversation.Id.Value,
            conversation.MessageCount,
            response.ToolExecutions);

        _logger.LogInformation(
            "Successfully processed message for conversation {ConversationId} with {MessageCount} total messages, {ToolCount} tool executions using {McpServerCount} MCP servers",
            conversation.Id.Value,
            conversation.MessageCount,
            response.ToolExecutions?.Length ?? 0,
            mcpServerCount);

        return enrichedResponse;
    }

    private async Task<Result<Conversation>> GetOrCreateConversationAsync(
        Guid? conversationId, 
        string userId, 
        CancellationToken cancellationToken)
    {
        if (conversationId.HasValue)
        {
            // Load existing conversation
            var existingResult = await _conversationRepository.GetAggregateAsync(
                ConversationId.From(conversationId.Value), cancellationToken);
            
            if (existingResult.IsFailure)
                return existingResult.Error;

            if (existingResult.Value is null)
                return Error.NotFound($"Conversation with ID {conversationId.Value} not found");

            var existingConversation = existingResult.Value;
            
            // Verify user ownership
            if (!existingConversation.BelongsToUser(userId))
                return Error.Forbidden("Access denied to conversation");

            // Ensure conversation can accept messages
            if (!existingConversation.CanAcceptMessages())
                return Error.Validation("Cannot add messages to inactive conversation");

            return existingConversation;
        }
        else
        {
            // Create new conversation with auto-generated title
            var title = GenerateConversationTitle();
            var newConversationResult = Conversation.Create(title, userId);
            if (newConversationResult.IsFailure)
                return newConversationResult.Error;

            var newConversation = newConversationResult.Value;
            var addResult = await _conversationRepository.AddAsync(newConversation, cancellationToken);
            if (addResult.IsFailure)
                return addResult.Error;

            return newConversation;
        }
    }

    private static string GenerateConversationTitle()
    {
        // Simple title generation for MVP - could be enhanced later with AI-based titles
        return $"New Conversation - {DateTime.UtcNow:yyyy-MM-dd HH:mm}";
    }
}