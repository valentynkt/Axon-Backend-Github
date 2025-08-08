using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Application.Repositories;
using Axon.Modules.Chat.Domain.Conversation;
using Axon.Shared.Common;
using Axon.Shared.Common.Abstractions;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Pagination;
using MediatR;

namespace Axon.Modules.Chat.Application.Queries.GetConversationHistory;

/// <summary>
/// Handler for getting conversation history with pagination
/// </summary>
public sealed class GetConversationHistoryHandler : IRequestHandler<GetConversationHistoryQuery, Result<GetConversationHistoryResponse>>
{
    private readonly IConversationRepository _conversationRepository;

    public GetConversationHistoryHandler(IConversationRepository conversationRepository)
    {
        _conversationRepository = conversationRepository;
    }

    public async Task<Result<GetConversationHistoryResponse>> HandleAsync(
        GetConversationHistoryQuery request, 
        CancellationToken cancellationToken)
    {
        Result<PagedResult<Conversation>> conversationsResult;

        // Get conversations based on status filter
        if (!string.IsNullOrEmpty(request.Status))
        {
            if (!Enum.TryParse<ConversationStatus>(request.Status, ignoreCase: true, out var status))
                return Error.Validation($"Invalid conversation status: {request.Status}");

            conversationsResult = await _conversationRepository.GetByStatusAsync(
                status, 
                (request.Skip / request.Take) + 1, // Convert skip to page number
                request.Take, 
                cancellationToken);
        }
        else
        {
            // Get recent conversations if no status filter
            conversationsResult = await _conversationRepository.GetRecentAsync(
                (request.Skip / request.Take) + 1, // Convert skip to page number
                request.Take,
                cancellationToken);
        }
        
        if (conversationsResult.IsFailure)
            return conversationsResult.Error;
            
        var pagedResult = conversationsResult.Value;
        var conversations = pagedResult.Items;

        // Map to DTOs
        var conversationDtos = conversations.Select(c =>
        {
            var lastMessage = c.MessagesOrdered.LastOrDefault();
            return new ConversationSummaryDto(
                c.Id,
                c.Title,
                c.Status.ToString(),
                c.CreatedAt,
                c.CompletedAt,
                c.MessageCount,
                lastMessage?.Content,
                lastMessage?.CreatedAt);
        }).ToList();

        // Calculate pagination info from paged result
        var totalCount = pagedResult.TotalCount;
        var hasNextPage = pagedResult.HasNextPage;

        var response = new GetConversationHistoryResponse(
            conversationDtos,
            totalCount,
            request.Skip,
            request.Take,
            hasNextPage);

        return response;
    }
}