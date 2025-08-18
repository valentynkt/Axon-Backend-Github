using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.Errors;
using Axon.Modules.Chat.Infrastructure.Ai.Abstractions;
using BuildingBlocks.Core.Functional.Results;

namespace Axon.Modules.Chat.Infrastructure.Ai.Services;

/// <summary>
/// Infrastructure error mapping service
/// </summary>
public sealed class ErrorMappingService : IErrorMappingService
{
    public Result<AiResponse> MapProcessingException(Exception exception)
    {
        return exception switch
        {
            HttpRequestException => ChatErrors.AiClient.Unavailable,
            TimeoutException => ChatErrors.AiClient.ProcessingTimeout,
            UnauthorizedAccessException => ChatErrors.AiClient.Unavailable,
            _ => ChatErrors.AiClient.InvalidResponse
        };
    }
}