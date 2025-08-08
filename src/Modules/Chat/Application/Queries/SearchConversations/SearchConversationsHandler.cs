using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Application.Repositories;
using Axon.Modules.Chat.Domain.Conversation;
using Axon.Shared.Common;
using Axon.Shared.Common.Abstractions;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Pagination;
using MediatR;

namespace Axon.Modules.Chat.Application.Queries.SearchConversations;

/// <summary>
/// Handler for searching conversations with filtering and pagination
/// </summary>
public sealed class SearchConversationsHandler : IRequestHandler<SearchConversationsQuery, Result<SearchConversationsResponse>>
{
    private readonly IConversationRepository _conversationRepository;

    public SearchConversationsHandler(IConversationRepository conversationRepository)
    {
        _conversationRepository = conversationRepository;
    }

    public async Task<Result<SearchConversationsResponse>> HandleAsync(
        SearchConversationsQuery request, 
        CancellationToken cancellationToken)
    {
        // Note: This is a simplified implementation. In a real-world scenario,
        // we would implement proper search functionality in the repository layer
        // with database-level filtering and full-text search capabilities.

        Result<PagedResult<Conversation>> conversationsResult;

        // Get conversations based on filters
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
            // Get recent conversations - note: filtering would be done in repository in real implementation
            conversationsResult = await _conversationRepository.GetRecentAsync(
                (request.Skip / request.Take) + 1, // Convert skip to page number
                request.Take,
                cancellationToken);
        }
        
        if (conversationsResult.IsFailure)
            return conversationsResult.Error;
            
        var pagedResult = conversationsResult.Value;
        var conversations = pagedResult.Items.Where(c => ApplyFilters(c, request)).ToList();

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

        var response = new SearchConversationsResponse(
            conversationDtos,
            totalCount,
            request.Skip,
            request.Take,
            hasNextPage,
            request.SearchTerm);

        return response;
    }

    private static bool ApplyFilters(Conversation conversation, SearchConversationsQuery request)
    {
        // Search term filter (title and message content)
        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            var searchTermLower = request.SearchTerm.ToLowerInvariant();
            var titleMatch = conversation.Title.ToLowerInvariant().Contains(searchTermLower);
            var messageMatch = conversation.MessagesOrdered
                .Any(m => m.Content.ToLowerInvariant().Contains(searchTermLower));
            
            if (!titleMatch && !messageMatch)
                return false;
        }

        // Date filters
        if (request.CreatedAfter.HasValue && conversation.CreatedAt < request.CreatedAfter.Value)
            return false;

        if (request.CreatedBefore.HasValue && conversation.CreatedAt > request.CreatedBefore.Value)
            return false;

        return true;
    }
}