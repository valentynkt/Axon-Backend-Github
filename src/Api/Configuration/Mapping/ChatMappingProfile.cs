using Mapster;
using Axon.Api.Contracts.V1.Chat;
using Axon.Modules.Chat.Application.Common;
using Axon.Modules.Chat.Application.DTOs.Requests;
using Axon.Modules.Chat.Application.DTOs.Responses;

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
    }
}