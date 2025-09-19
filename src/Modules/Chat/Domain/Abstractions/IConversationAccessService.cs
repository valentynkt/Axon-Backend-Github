using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Chat.Domain.Abstractions;

/// <summary>
/// Domain service interface for managing conversation access validation.
/// Encapsulates business rules around conversation ownership and permissions.
/// Implementation will be in the Application or Infrastructure layer to avoid circular dependencies.
/// </summary>
public interface IConversationAccessService
{
    /// <summary>
    /// Validates that a user has access to a specific conversation.
    /// Returns domain error if access is denied.
    /// </summary>
    /// <param name="conversationId">The conversation to validate access to</param>
    /// <param name="userId">The user requesting access</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success if user has access, domain error otherwise</returns>
    Task<Result<bool, Error>> ValidateAccessAsync(
        ConversationId conversationId, 
        AxonUserId userId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user can access a conversation without throwing exceptions.
    /// </summary>
    /// <param name="conversationId">The conversation to check access to</param>
    /// <param name="userId">The user requesting access</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user has access, false otherwise</returns>
    Task<bool> CanAccessAsync(
        ConversationId conversationId, 
        AxonUserId userId, 
        CancellationToken cancellationToken = default);
}