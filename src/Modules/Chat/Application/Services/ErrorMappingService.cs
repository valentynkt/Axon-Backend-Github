using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.DTOs;
using BuildingBlocks.Core.Functional.Results;

namespace Axon.Modules.Chat.Application.Services;

/// <summary>
/// Simple error mapping service implementation
/// </summary>
public sealed class ErrorMappingService : IErrorMappingService
{
    public Result<AiResponse> MapException(Exception exception)
    {
        var errorMessage = exception switch
        {
            HttpRequestException => "External service unavailable",
            TimeoutException => "Request timeout",
            _ => "An error occurred processing the request"
        };
        
        return Result<AiResponse>.Failure(Error.ExternalService(errorMessage));
    }
}