using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.DTOs;

namespace Axon.Modules.Chat.Application.Abstractions;

/// <summary>
/// Service for mapping AI responses to API responses
/// </summary>
public interface IResponseMappingService
{
    /// <summary>
    /// Maps an AI response to a process message response
    /// </summary>
    /// <param name="aiResponse">AI response to map</param>
    /// <returns>Mapped process message response</returns>
    ProcessMessageResponse MapToApiResponse(AiResponse aiResponse);
}