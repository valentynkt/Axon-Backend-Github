using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Application.Services;

/// <summary>
/// Service responsible for mapping AI responses to API responses
/// </summary>
public sealed class ResponseMappingService : IResponseMappingService
{
    /// <summary>
    /// Maps an AI response to a process message response
    /// </summary>
    /// <param name="aiResponse">AI response to map</param>
    /// <returns>Mapped process message response</returns>
    public ProcessMessageResponse MapToApiResponse(AiResponse aiResponse)
    {
        ArgumentNullException.ThrowIfNull(aiResponse);

        // Generate conversation ID if not provided in response or if provided value is not a valid GUID
        var conversationId = string.IsNullOrEmpty(aiResponse.ResponseId) || !Guid.TryParse(aiResponse.ResponseId, out var parsedGuid) 
            ? ConversationId.New().Value 
            : parsedGuid;
        
        // Map tool executions to summaries
        var toolSummaries = aiResponse.ToolExecutions?.Select(tool =>
            new ToolExecutionSummary(
                ToolName: tool.ToolName,
                Success: tool.IsSuccess,
                Duration: tool.ExecutionTime)).ToArray();

        return new ProcessMessageResponse(
            Response: aiResponse.Content,
            ConversationId: conversationId,
            MessageCount: 0,
            ToolExecutions: toolSummaries);
    }
}