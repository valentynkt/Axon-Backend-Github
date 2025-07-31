---
id: AXON-20250731-Chat-API_CONTRACT
title: Chat: API Contract
module: Chat
feature: Direct_MCP
gate: Ship
owner: valentynkit
status: approved
relates_to: [AXON-20250731-Chat-Direct_MCP-PR_BODY]
source_of_truth: doc
created: 2025-07-29
updated: 2025-07-31
version: 2
---

# Endpoints

## POST /api/chat/process
**Purpose**: Process chat messages with Direct MCP tool execution  
**Method**: POST  
**Content Type**: application/json

**Request**: `ProcessMessageRequest`
```csharp
public sealed record ProcessMessageRequest(
    string Message,
    string? ConversationId = null);
```

**Response**: `ProcessMessageResponse`
```csharp
public sealed record ProcessMessageResponse(
    string Response,
    string ConversationId,
    ToolExecutionResponse[]? ToolExecutions = null);

public sealed record ToolExecutionResponse(
    string ToolName,
    bool Success,
    int DurationMs);
```

# DTOs

## ProcessMessageRequest
- `Message` (required): User message content
- `ConversationId` (optional): Continuation of existing conversation

## ProcessMessageResponse  
- `Response` (required): AI-generated response with tool results
- `ConversationId` (required): Conversation identifier for context
- `ToolExecutions` (optional): Array of executed tool information

## ToolExecutionResponse
- `ToolName` (required): Name of the executed tool
- `Success` (required): Whether tool execution succeeded
- `DurationMs` (required): Execution time in milliseconds

# Errors & Result Mapping

| HTTP Status | Error Code | Description |
|-------------|------------|-------------|
| 400 | Bad Request | Invalid message content or validation failure |
| 422 | Unprocessable Entity | Business rule violation |
| 500 | Internal Server Error | External service failure or unexpected error |
| 503 | Service Unavailable | OpenAI API or MCP server unavailable |

# Versions & Compatibility

**Current Version**: v1.0  
**Breaking Changes**: None  
**Backward Compatibility**: Full compatibility maintained

# Changelog

## v2 (2025-07-31)
- **Internal Change**: Migrated from OpenAI Chat Completions to Responses API for Direct MCP
- **API Surface**: No changes to endpoints, DTOs, or error handling
- **Performance**: Improved response times for tool-based queries (<2s target)

## v1 (2025-07-29) 
- Initial Chat API with MCP tool execution support
- ProcessMessage endpoint with FastEndpoints implementation
- Tool execution reporting in responses

# Breaking Changes

None - All changes in v2 are internal implementation improvements that maintain full API compatibility.