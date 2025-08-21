using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Domain.Types;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Application.DTOs;

/// <summary>
/// AI response with tool execution results
/// </summary>
public sealed record AiResponse(
    MessageContent Content,
    AiResponseId ResponseId,
    ToolExecution[]? ToolExecutions = null);