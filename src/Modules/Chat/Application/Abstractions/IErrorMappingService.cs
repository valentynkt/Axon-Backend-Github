using Axon.Modules.Chat.Application.DTOs;
using BuildingBlocks.Core.Functional.Results;

namespace Axon.Modules.Chat.Application.Abstractions;

/// <summary>
/// Maps errors for application layer
/// </summary>
public interface IErrorMappingService
{
    /// <summary>
    /// Map exception to Result
    /// </summary>
    Result<AiResponse> MapException(Exception exception);
}