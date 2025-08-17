using Axon.Modules.Chat.Application.DTOs;
using BuildingBlocks.Core.Functional.Results;

namespace Axon.Modules.Chat.Application.Abstractions.AI;

/// <summary>
/// Service for mapping and validating AI responses
/// </summary>
public interface IResponseMappingService
{
    /// <summary>
    /// Validates and processes AI response for conversation use
    /// </summary>
    /// <param name="aiResponse">AI response from external service</param>
    /// <returns>Result indicating validation success and any processing outcome</returns>
    Result<AiResponse> ValidateAndProcessResponse(AiResponse aiResponse);
}