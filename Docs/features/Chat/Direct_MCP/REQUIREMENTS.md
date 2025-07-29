---
id: AXON-20250729-Chat-Direct_MCP-REQUIREMENTS
title: Direct_MCP: Requirements
module: Chat
feature: Direct_MCP
gate: G1
owner: <owner>
status: approved
relates_to: []
source_of_truth: doc
created: 2025-07-29
updated: 2025-07-29
version: 1
---

# Problem

Current chat systems require the application to mediate between the LLM and external tools, creating latency, token overhead, and complex orchestration logic. Users experience slow responses due to multiple round-trips (App ↔ LLM ↔ App executes tool ↔ App replies), especially when multiple tool calls are needed. The application becomes a bottleneck for tool execution rather than focusing on business logic.

**Primary User**: Developers and end-users interacting with AI chat that needs access to external tools (weather, web search, databases, etc.)  
**Measurable Pain**: >2 second response times for simple tool-based queries; complex error handling for tool orchestration failures.

# Business Goal

Enable direct communication between OpenAI's language models and remote MCP (Model Context Protocol) servers, reducing response latency by 50% and eliminating application-mediated tool orchestration complexity. Success is measured by:
- Chat responses that include tool results in <2 seconds for single tool calls
- Zero manual tool orchestration code in the application layer
- Successful tool discovery and execution without application intervention

# Acceptance Criteria

## AC1: Direct MCP Tool Discovery
**GIVEN** a configured remote MCP server URL with available tools  
**WHEN** a user sends a chat message that could benefit from external tools  
**THEN** the system should automatically discover available tools from the MCP server via OpenAI's Responses API  
**AND** tool schemas should be cached in the conversation context for subsequent use

## AC2: Direct Tool Execution
**GIVEN** a user query that requires external tool usage (e.g., "What's the weather in Boston?")  
**WHEN** the OpenAI model determines a tool call is needed  
**THEN** OpenAI should directly call the remote MCP server without application mediation  
**AND** the tool result should be incorporated into the final response  
**AND** the complete response should be returned to the user within 2 seconds

## AC3: Clean Architecture Integration
**GIVEN** the existing Chat module structure (Application/Domain/Infrastructure)  
**WHEN** implementing Direct MCP functionality  
**THEN** the solution should follow CQRS patterns with a ProcessMessageCommand and Handler  
**AND** external MCP integration should be abstracted behind an IAiClient port in Application  
**AND** OpenAI SDK usage should be isolated to Infrastructure layer only

## AC4: Error Handling
**GIVEN** a remote MCP server is unreachable or returns errors  
**WHEN** a tool call is attempted  
**THEN** the system should return a meaningful error message to the user  
**AND** the conversation should remain functional for non-tool-based queries  
**AND** errors should be logged with sufficient context for debugging

## AC5: Security and Configuration
**GIVEN** remote MCP servers require authentication  
**WHEN** configuring the Direct MCP feature  
**THEN** authentication headers should be passed securely to the MCP server  
**AND** server URLs and credentials should be configurable via appsettings  
**AND** no sensitive data should be logged in responses

# Constraints

**Performance Requirements**:
- Single tool call responses: <2 seconds end-to-end
- Tool discovery (first call): <5 seconds with schema caching
- Memory usage: <50MB additional per conversation context

**Technical Constraints**:
- Must use .NET 10 (preview) with nullable reference types enabled
- Must follow Clean Architecture dependency rules (API → Application → Domain)
- Must use OpenAI Responses API only (no custom tool orchestration)
- Must implement CQRS pattern using MediatR
- Must use Result<T> pattern for error handling (no exceptions for business logic)

**Security Requirements**:
- MCP server authentication via headers only
- No storage of credentials in OpenAI context
- Path redaction in responses enforced by OpenAI
- Audit logging of all tool calls and results

**Compatibility**:
- Must work with existing Chat module structure
- Must not break any existing API contracts
- Must support both streaming and non-streaming responses (MVP: non-streaming only)

# Non‑Goals

**Explicitly out of scope for MVP**:
- Tool approval workflows (require_approval: false for MVP)
- Multi-turn conversation persistence across sessions
- Streaming responses (Phase 2)
- Custom MCP server implementation (use existing external servers)
- Tool result caching beyond OpenAI's built-in context caching
- Multiple MCP server support (single server per conversation)
- Fine-grained tool filtering (allowed_tools) - MVP uses all available tools
- Conversation state management beyond single request/response

# Assumptions & Risks

**Assumptions**:
- External MCP servers are already available and accessible (weather, web search, etc.)
- OpenAI Responses API with MCP support is stable enough for production use
- Network latency between OpenAI and MCP servers is reasonable (<500ms)
- MCP servers follow the protocol specification correctly

**Risks**:
- **Functional Risk**: OpenAI Responses API MCP integration is in preview and may have breaking changes
  *Mitigation*: Pin OpenAI SDK versions, monitor release notes
- **Operational Risk**: External MCP server failures could impact chat functionality
  *Mitigation*: Implement graceful degradation, clear error messages
- **Security Risk**: Remote MCP servers could potentially access conversation context
  *Mitigation*: Only connect to trusted MCP servers, implement audit logging
- **Performance Risk**: Tool discovery latency could exceed SLA on first use
  *Mitigation*: Implement conversation context caching via previous_response_id

# Open Questions

1. **MCP Server Selection**: Which specific external MCP servers should be configured for MVP? (weather, web search, calculator?)
2. **Authentication Strategy**: What authentication method will external MCP servers require? (API keys, OAuth, none?)
3. **Error Fallback**: Should the system attempt to continue conversation without tools when MCP fails, or return explicit error?
4. **Conversation Context**: How long should OpenAI conversation context be maintained for tool schema caching?
5. **Configuration Management**: Should MCP server URLs be configurable per user/tenant or globally?

*Suggest orchestrator route to docs-grounder for external MCP server availability and authentication requirements.*