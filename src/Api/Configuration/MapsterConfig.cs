using Mapster;
using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.Commands.ProcessMessage.Dtos;
using Axon.Api.Contracts.V1.Chat;
using Axon.BuildingBlocks.Core.Functional.Results;
using Axon.Modules.Chat.Application.Common;

namespace Axon.Api.Configuration;

/// <summary>
/// Mapster configuration for the API layer
/// </summary>
public static class MapsterConfig
{
    /// <summary>
    /// Configure Mapster type adapters
    /// </summary>
    public static void Configure()
    {
        // Global settings
        TypeAdapterConfig.GlobalSettings.Default
            .NameMatchingStrategy(NameMatchingStrategy.Flexible)
            .PreserveReference(true);

        // Configure chat mappings
        ConfigureChatMappings();
    }

    private static void ConfigureChatMappings()
    {
        // Request: ChatTurnRequestDto -> ProcessMessageRequest
        TypeAdapterConfig<ChatTurnRequestDto, ProcessMessageRequest>
            .NewConfig()
            .Map(dest => dest.ConversationId, src => src.ConversationId)
            .Map(dest => dest.Message, src => src.Message);

        // Response: ProcessMessageResponse -> ChatTurnResponseDto
        TypeAdapterConfig<ProcessMessageResponse, ChatTurnResponseDto>
            .NewConfig()
            .Map(dest => dest.ConversationId, src => src.ConversationId.Value)
            .Map(dest => dest.UserMessageId, src => src.UserMessageId.Value)
            .Map(dest => dest.AssistantMessageId, src => src.AssistantMessageId.Value)
            .Map(dest => dest.AssistantMessage, src => src.AssistantMessage.ToString())
            .Map(dest => dest.Timestamp, src => DateTimeOffset.UtcNow);
    }
}