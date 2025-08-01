using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.DTOs;

namespace Axon.Modules.Chat.Application.Contracts;

/// <summary>
/// Contract for response mapping - to be implemented by srp-decomposition-specialist
/// </summary>
public interface IResponseMapper
{
    /// <summary>
    /// Map AI response to API response
    /// </summary>
    /// <param name="aiResponse">AI response from service</param>
    /// <returns>Mapped API response</returns>
    ProcessMessageResponse MapToApiResponse(AiResponse aiResponse);
}