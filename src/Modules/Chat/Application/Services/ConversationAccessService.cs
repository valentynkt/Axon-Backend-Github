using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Domain.Abstractions;
using Axon.Modules.Chat.Domain.Errors;
using Axon.Modules.Chat.Domain.Specifications;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Chat.Application.Services;

/// <summary>
/// Application service implementation for conversation access validation.
/// Uses domain specifications and business rules to enforce access control.
/// </summary>
public sealed class ConversationAccessService : IConversationAccessService
{
    private readonly IConversationReadRepository _conversationRepository;

    public ConversationAccessService(IConversationReadRepository conversationRepository)
    {
        _conversationRepository = conversationRepository;
    }

    public async Task<Result<bool, Error>> ValidateAccessAsync(
        ConversationId conversationId, 
        UserId userId, 
        CancellationToken cancellationToken = default)
    {
        var hasAccess = await CanAccessAsync(conversationId, userId, cancellationToken);
        
        if (!hasAccess)
        {
            return Result.Failure<bool, Error>(
                ChatErrors.Conversation.AccessDenied(conversationId.Value));
        }

        return Result.Success<bool, Error>(true);
    }

    public async Task<bool> CanAccessAsync(
        ConversationId conversationId, 
        UserId userId, 
        CancellationToken cancellationToken = default)
    {
        var accessSpec = new ConversationAccessSpec(conversationId, userId);
        return await _conversationRepository.AnyAsync(accessSpec, cancellationToken);
    }
}