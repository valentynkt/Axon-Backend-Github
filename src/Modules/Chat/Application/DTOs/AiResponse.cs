using Axon.Modules.Chat.Domain.Types;

namespace Axon.Modules.Chat.Application.DTOs;

/// <summary>
/// AI response with tool execution results
/// </summary>
public sealed record AiResponse(
    string Content,
    string? ResponseId = null,
    ToolExecution[]? ToolExecutions = null);