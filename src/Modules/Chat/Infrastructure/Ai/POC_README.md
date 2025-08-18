# Direct OpenAI MCP Integration - Proof of Concept

## Overview
This is an ultra-simple proof of concept implementation for Direct MCP (Model Context Protocol) integration with OpenAI's Responses API. The goal is to demonstrate end-to-end connectivity without complex application layer logic.

## Files Created
1. **SimpleOpenAiMcpClient.cs** - Core implementation of IAiClient that makes direct HTTP calls to OpenAI Responses API
2. **Test Endpoints** - Two test endpoints in `/api/chat/test-mcp` and `/api/chat/test-mcp-input`
3. **Tests** - Basic unit tests for the client implementation

## Configuration
Add the following to your `appsettings.Development.json`:

```json
{
  "Chat": {
    "UsePocMode": true,
    "OpenAi": {
      "ApiKey": "YOUR_OPENAI_API_KEY_HERE",
      "Model": "gpt-4o",
      "MaxTokens": 4000,
      "Temperature": 0.7,
      "McpEnabled": true,
      "TimeoutSeconds": 60
    }
  }
}
```

## How to Test

### 1. Set your OpenAI API Key
Either in appsettings.Development.json or as environment variable:
```bash
export OPENAI_API_KEY="your-api-key"
```

### 2. Run the API
```bash
dotnet run --project src/Api
```

### 3. Test the endpoints

#### Simple GET test:
```bash
curl http://localhost:5000/api/chat/test-mcp
```

#### POST with custom message:
```bash
curl -X POST http://localhost:5000/api/chat/test-mcp-input \
  -H "Content-Type: application/json" \
  -d '{
    "message": "What is the capital of France?",
    "useMcpServers": false
  }'
```

#### POST with MCP server (when available):
```bash
curl -X POST http://localhost:5000/api/chat/test-mcp-input \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Get the weather in Boston",
    "useMcpServers": true,
    "mcpServerUrl": "https://your-mcp-server.com/api/mcp"
  }'
```

## Architecture Flow
```
API Endpoint (TestMcpEndpoint)
    ↓
SimpleOpenAiMcpClient (Infrastructure)
    ↓
OpenAI Responses API (with Direct MCP)
    ↓
Response back to client
```

## Key Implementation Details

### SimpleOpenAiMcpClient
- Builds OpenAI Responses API request with MCP tool configuration
- Sends HTTP POST to `https://api.openai.com/v1/responses`
- Parses response to extract text content
- Returns Result<AiResponse> following the Result pattern

### Request Format
```json
{
  "model": "gpt-4o",
  "input": "User message here",
  "tools": [
    {
      "type": "mcp",
      "server_url": "https://mcp.server.com/api",
      "server_label": "MCP Server",
      "require_approval": false
    }
  ],
  "max_tokens": 4000,
  "temperature": 0.7
}
```

### Response Parsing
- Extracts text from `output[].content[].text` structure
- Stores `response_id` for conversation continuity
- Tool executions parsing can be added later

## Current Limitations (POC)
1. **No complex tool execution parsing** - Only text responses are extracted
2. **No application layer integration** - Direct infrastructure calls only
3. **Basic error handling** - Simple try-catch with error results
4. **No caching or optimization** - Focus on getting it working
5. **No real MCP server** - Need actual MCP server for full testing

## Next Steps
Once POC is validated:
1. Implement full tool execution parsing
2. Integrate with application layer Commands/Queries
3. Add comprehensive error handling and retry logic
4. Implement response caching with `previous_response_id`
5. Add proper observability with OpenTelemetry
6. Create production-ready implementation with all abstractions

## Testing Without Real MCP Server
The POC will still work without a real MCP server - OpenAI will simply not have any tools available and will respond with plain text. This allows testing the basic request/response flow.

## Known Issues
- The Infrastructure project has some pre-existing compilation issues with missing interfaces
- These don't affect the POC implementation which is self-contained
- Use `UsePocMode: true` in configuration to bypass the problematic implementations