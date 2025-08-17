using Axon.Modules.Chat.Application.Abstractions.AI;
using Axon.Modules.Chat.Application.DTOs;
using BuildingBlocks.Core.Functional.Results;

namespace Axon.Modules.Chat.Application.Services.AI.Processing;

/// <summary>
/// Service responsible for validating and processing AI responses
/// </summary>
public sealed class ResponseMappingService : IResponseMappingService
{
    /// <summary>
    /// Validates and processes AI response for conversation use
    /// </summary>
    /// <param name="aiResponse">AI response from external service</param>
    /// <returns>Result indicating validation success and any processing outcome</returns>
    public Result<AiResponse> ValidateAndProcessResponse(AiResponse aiResponse)
    {
        ArgumentNullException.ThrowIfNull(aiResponse);

        // Validate required fields
        if (string.IsNullOrWhiteSpace(aiResponse.Content))
        {
            return Result<AiResponse>.Failure(
                Error.Validation("AI response content cannot be empty", "CHAT.AI.EMPTY_CONTENT"));
        }

        if (string.IsNullOrWhiteSpace(aiResponse.ResponseId))
        {
            return Result<AiResponse>.Failure(
                Error.Validation("AI response must include a response ID", "CHAT.AI.MISSING_RESPONSE_ID"));
        }

        // Response is valid, return as-is
        return Result<AiResponse>.Success(aiResponse);
    }
}