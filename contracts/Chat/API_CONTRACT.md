# Chat Module API Contract

## Overview
The Chat module provides AI-powered message processing with optional MCP (Model Context Protocol) tool integration.

## Endpoints

### POST /api/chat/process
Process a chat message with optional MCP tool integration.

**Request Body**: `ProcessMessageRequest`
```json
{
  "message": "string (required)",
  "mcpServer": {
    "serverUrl": "string (required)",
    "serverLabel": "string (optional)",
    "headers": {
      "key": "value"
    },
    "allowedTools": ["tool1", "tool2"]
  },
  "conversationId": "string (optional)"
}
```

**Response**: `ProcessMessageResponse`
```json
{
  "response": "string",
  "conversationId": "string", 
  "toolExecutions": [
    {
      "toolName": "string",
      "success": "boolean",
      "durationMs": "integer"
    }
  ]
}
```

**Status Codes**:
- `200 OK`: Success
- `400 Bad Request`: Validation error  
- `404 Not Found`: Resource not found
- `502 Bad Gateway`: External MCP service error
- `500 Internal Server Error`: Unexpected server error

## Data Transfer Objects

### ProcessMessageRequest
- `Message` (string, required): User message to process
- `McpServer` (McpServerRequest, optional): MCP server configuration
- `ConversationId` (string, optional): Conversation identifier for context

### ProcessMessageResponse  
- `Response` (string, required): AI-generated response
- `ConversationId` (string, required): Conversation identifier
- `ToolExecutions` (ToolExecutionResponse[], optional): Executed tool results

### McpServerRequest
- `ServerUrl` (string, required): MCP server endpoint URL
- `ServerLabel` (string, optional): Human-readable server name
- `Headers` (Dictionary<string,string>, optional): Custom headers for MCP requests
- `AllowedTools` (string[], optional): Whitelist of allowed tools

### ToolExecutionResponse
- `ToolName` (string, required): Name of executed tool
- `Success` (boolean, required): Execution success status  
- `DurationMs` (integer, required): Execution time in milliseconds

## Change History
- **2025-07-29**: Initial Chat module API contract - NEW endpoint, non-breaking