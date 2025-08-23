using Mapster;
using Axon.Api.Contracts.V1.Chat;
using Axon.Modules.Chat.Application.Common;

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
        config.NewConfig<ProcessMessageResponse, ChatTurnResponseDto>()
            .Map(dest => dest.ConversationId, src => src.ConversationId.Value)
            .Map(dest => dest.UserMessageId, src => src.UserMessageId.Value)
            .Map(dest => dest.AssistantMessageId, src => src.AssistantMessageId.Value)
            .Map(dest => dest.AssistantMessage, src => src.AssistantMessage.ToString()!)
            .Map(dest => dest.Timestamp, src => DateTimeOffset.UtcNow)
            .IgnoreNonMapped(true);
    }
}