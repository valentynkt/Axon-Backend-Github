using Axon.Modules.Chat.Application.DTOs.Requests;
using Axon.Modules.Chat.Application.DTOs.Responses;
using CSharpFunctionalExtensions;
using BuildingBlocks.Core.Diagnostics.Errors;

namespace Axon.Modules.Chat.Application.Contracts.Dispatching;

/// <summary>
/// Dispatches chat requests to appropriate commands based on business rules.
/// </summary>
public interface IChatCommandDispatcher
{
    /// <summary>
    /// Processes a chat message request by dispatching to the appropriate command.
    /// Result-based contract carries success/failure (no extra status field needed).
    /// </summary>
    Task<Result<ProcessMessageResponse, Error>> ProcessMessageAsync(
        ProcessMessageRequest request,
        CancellationToken cancellationToken = default);
}