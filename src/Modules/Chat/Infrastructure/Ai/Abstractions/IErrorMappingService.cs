using Axon.Modules.Chat.Application.DTOs;
using BuildingBlocks.Core.Functional.Results;

namespace Axon.Modules.Chat.Infrastructure.Ai.Abstractions;

/// <summary>
/// Maps exceptions to Result errors
/// </summary>
public interface IErrorMappingService
{
    /// <summary>
    /// Map processing exception to Result
    /// </summary>
    Result<AiResponse> MapProcessingException(Exception exception);
}