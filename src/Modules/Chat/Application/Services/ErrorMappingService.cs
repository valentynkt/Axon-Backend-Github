using System.Net.Http;
using System.Text.Json;
using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Domain.Errors;
using Axon.Shared.Common;

namespace Axon.Modules.Chat.Application.Services;

/// <summary>
/// Implementation of error mapping service maintaining clean architecture boundaries
/// </summary>
public sealed class ErrorMappingService : IErrorMappingService
{
    public Error MapProcessingException(Exception exception)
    {
        return exception switch
        {
            HttpRequestException => ChatErrors.AiClient.Unavailable,
            TimeoutException => ChatErrors.AiClient.ProcessingTimeout,
            UnauthorizedAccessException => ChatErrors.AiClient.Unavailable,
            JsonException => ChatErrors.AiClient.InvalidResponse,
            _ => ChatErrors.AiClient.InvalidResponse
        };
    }

    public Error MapHttpRequestException(Exception exception)
    {
        return ChatErrors.AiClient.Unavailable;
    }

    public Error MapTimeoutException(Exception exception)
    {
        return ChatErrors.AiClient.ProcessingTimeout;
    }

    public Error MapUnauthorizedAccessException(Exception exception)
    {
        return ChatErrors.AiClient.Unavailable;
    }

    public Error MapJsonException(Exception exception)
    {
        return ChatErrors.AiClient.InvalidResponse;
    }

    public Error MapGenericException(Exception exception)
    {
        return ChatErrors.AiClient.InvalidResponse;
    }
}