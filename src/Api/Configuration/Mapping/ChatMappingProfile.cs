using Mapster;
using Axon.Api.Contracts.V1.Chat;
using Axon.Modules.Chat.Application.Common;
using Axon.Modules.Chat.Application.Common.Pagination;
using Axon.Modules.Chat.Application.Common.Sorting;
using Axon.Modules.Chat.Application.DTOs.Requests;
using Axon.Modules.Chat.Application.DTOs.Responses;
using Axon.Modules.Chat.Application.Queries.GetConversations;
using Axon.Modules.Chat.Application.Queries.GetConversationMessages;

namespace Axon.Api.Configuration.Mapping;

/// <summary>
/// Mapping profile for Chat module API contracts
/// </summary>
public sealed class ChatMappingProfile : IRegister, IChatMappingProfile
{
    public string ProfileName => "ChatAPI";

    public void Register(TypeAdapterConfig config)
    {
        ConfigureRequestMappings(config);
        ConfigureResponseMappings(config);
    }

    private static void ConfigureRequestMappings(TypeAdapterConfig config)
    {
        // ChatTurnRequestDto -> ProcessMessageRequest
        config.NewConfig<ChatTurnRequestDto, ProcessMessageRequest>()
            .Map(dest => dest.ConversationId, src => src.ConversationId)
            .Map(dest => dest.Message, src => src.Message)
            .IgnoreNonMapped(true);

        // GetConversationsRequestDto -> GetConversationsQuery
        config.NewConfig<GetConversationsRequestDto, GetConversationsQuery>()
            .ConstructUsing(src => new GetConversationsQuery(
                src.PageNumber ?? 1,
                src.PageSize ?? Page.DefaultSize,
                ParseSortBy(src.SortBy),
                ParseSortDirection(src.SortDirection),
                src.TitleContains
            ));

        // GetConversationMessagesRequestDto -> GetConversationMessagesQuery
        config.NewConfig<GetConversationMessagesRequestDto, GetConversationMessagesQuery>()
            .ConstructUsing(src => new GetConversationMessagesQuery(
                src.ConversationId,  // Non-nullable from route
                src.PageNumber ?? 1,
                src.PageSize ?? Page.DefaultSize,
                src.IncludeDeleted ?? false
            ));
    }

    private static void ConfigureResponseMappings(TypeAdapterConfig config)
    {
        // ProcessMessageResponse -> ChatTurnResponseDto
        // Using MapWith to handle strongly-typed IDs more reliably
        config.NewConfig<ProcessMessageResponse, ChatTurnResponseDto>()
            .MapWith(src => new ChatTurnResponseDto
            {
                ConversationId = src.ConversationId.Value,
                UserMessageId = src.UserMessageId.Value,
                AssistantMessageId = src.AssistantMessageId.Value,
                AssistantMessage = src.AssistantMessage.Value,
                Timestamp = DateTimeOffset.UtcNow
            });

        // ConversationListItem -> ConversationItemDto
        config.NewConfig<ConversationListItem, ConversationItemDto>()
            .Map(dest => dest.ConversationId, src => src.ConversationId)
            .Map(dest => dest.Title, src => src.Title)
            .Map(dest => dest.CreatedAtUtc, src => src.CreatedAtUtc)
            .Map(dest => dest.UpdatedAtUtc, src => src.UpdatedAtUtc)
            .Map(dest => dest.LastAssistantResponseId, src => src.LastAssistantResponseId);

        // Paged<ConversationListItem> -> GetConversationsResponseDto
        config.NewConfig<Paged<ConversationListItem>, GetConversationsResponseDto>()
            .Map(dest => dest.Items, src => src.Items.Adapt<List<ConversationItemDto>>())
            .Map(dest => dest.PageNumber, src => src.PageNumber)
            .Map(dest => dest.PageSize, src => src.PageSize)
            .Map(dest => dest.TotalCount, src => src.TotalCount)
            .Map(dest => dest.TotalPages, src => src.TotalPages)
            .Map(dest => dest.HasPrevious, src => src.HasPrevious)
            .Map(dest => dest.HasNext, src => src.HasNext)
            .Map(dest => dest.Count, src => src.Count)
            .Map(dest => dest.IsEmpty, src => src.IsEmpty)
            .Map(dest => dest.FirstItemIndex, src => src.FirstItemIndex)
            .Map(dest => dest.LastItemIndex, src => src.LastItemIndex);

        // ConversationMessageItem -> ConversationMessageDto
        config.NewConfig<ConversationMessageItem, ConversationMessageDto>()
            .Map(dest => dest.MessageId, src => src.MessageId)
            .Map(dest => dest.Role, src => src.Role)
            .Map(dest => dest.Content, src => src.Content)
            .Map(dest => dest.CreatedAtUtc, src => src.CreatedAtUtc)
            .Map(dest => dest.Sequence, src => src.Sequence);

        // Paged<ConversationMessageItem> -> GetConversationMessagesResponseDto
        config.NewConfig<Paged<ConversationMessageItem>, GetConversationMessagesResponseDto>()
            .Map(dest => dest.Items, src => src.Items.Adapt<List<ConversationMessageDto>>())
            .Map(dest => dest.PageNumber, src => src.PageNumber)
            .Map(dest => dest.PageSize, src => src.PageSize)
            .Map(dest => dest.TotalCount, src => src.TotalCount)
            .Map(dest => dest.TotalPages, src => src.TotalPages)
            .Map(dest => dest.HasPrevious, src => src.HasPrevious)
            .Map(dest => dest.HasNext, src => src.HasNext)
            .Map(dest => dest.Count, src => src.Count)
            .Map(dest => dest.IsEmpty, src => src.IsEmpty)
            .Map(dest => dest.FirstItemIndex, src => src.FirstItemIndex)
            .Map(dest => dest.LastItemIndex, src => src.LastItemIndex);
    }

    /// <summary>
    /// Parses string value to ConversationSortBy enum with fallback to default.
    /// </summary>
    /// <param name="value">The string value to parse</param>
    /// <returns>The parsed enum value or default if invalid</returns>
    private static ConversationSortBy ParseSortBy(string? value) =>
        Enum.TryParse<ConversationSortBy>(value, ignoreCase: true, out var result) 
            ? result 
            : ConversationSortBy.UpdatedAt;

    /// <summary>
    /// Parses string value to SortDirection enum with fallback to default.
    /// </summary>
    /// <param name="value">The string value to parse</param>
    /// <returns>The parsed enum value or default if invalid</returns>
    private static SortDirection ParseSortDirection(string? value) =>
        Enum.TryParse<SortDirection>(value, ignoreCase: true, out var result) 
            ? result 
            : SortDirection.Desc;
}