---
id: AXON-20250130-chat-processmessage-RESEARCH_NOTES
title: ProcessMessage: OpenAI MCP Integration Research Notes
module: Chat
feature: ProcessMessage
gate: G1
owner: system
status: draft
relates_to: []
source_of_truth: doc
created: 2025-01-30
updated: 2025-01-30
version: 1
---

# Questions
- Is the OpenAI Responses API with MCP tool support actually available and stable?
- What is the current syntax and structure for MCP tool definitions in API requests?
- Are there any recent changes or updates to the MCP integration?
- What are the exact endpoints and request formats needed?

# Findings (evidence-backed)
- **MCP Support Confirmed**: OpenAI officially added remote MCP server support to Responses API in May 2025 — [openai_cookbook]
- **Production Ready**: MCP integration is stable and production-ready with no additional markup costs — [context7_platform]
- **Direct Integration**: Models interact directly with MCP servers, reducing latency vs traditional function calling — [openai_cookbook]
- **Standard Endpoint**: Uses `/v1/responses` endpoint with `tools` array containing MCP configuration — [openai_cookbook]
- **Auto-Discovery**: Runtime auto-detects transport protocol (HTTP/SSE) and fetches tools via `/tools/list` — [openai_cookbook]
- **Security Model**: Headers and server URLs are discarded after each request for security — [openai_cookbook]
- **Tool Filtering**: `allowed_tools` parameter limits exposed tools and reduces payload size — [openai_cookbook]
- **Approval Control**: `require_approval: "never"` disables user approval requirement for tool execution — [openai_cookbook]

# Apply vs Not-Apply (Axon-specific)
- **Apply**: Direct MCP integration in ProcessMessage endpoint for external tool connectivity
  - Use in `src/Api/Endpoints/Chat/ProcessMessage/ProcessMessageEndpoint.cs`
  - Configure MCP servers in `appsettings.json` (align with existing MCP config migration)
  - Implement in Application layer `ProcessMessageHandler` with Result pattern
- **Not-Apply**: Manual function calling orchestration (MCP eliminates this complexity)
  - Don't build custom tool discovery/execution pipelines
  - Avoid complex webhook coordination for chained external APIs

# Assumptions & Risks
- **Assumption**: Current MCP configuration in appsettings.json can be adapted for Responses API format
- **Risk**: External MCP server availability impacts ProcessMessage reliability
- **Assumption**: OpenAI's token billing model applies to MCP tool executions (no hidden costs)

# Contradictions / Gaps
- **Gap**: No specific error handling patterns for MCP server failures in documentation
- **Gap**: Rate limiting behavior for MCP server calls not clearly documented
- **Contradiction**: Some sources suggest MCP is "new" but adoption appears widespread (ChatGPT, Agents SDK)

# Citations
- [openai_cookbook]: OpenAI Cookbook MCP Tool Guide — https://github.com/openai/openai-cookbook/blob/main/examples/mcp/mcp_tool_guide.ipynb (context7)
- [context7_platform]: OpenAI Platform Documentation — Context7 platform_openai library (context7)
- [web_search]: OpenAI Responses API MCP Updates — Web search results January 2025 (websearch)

# Confidence
High — Official OpenAI documentation confirms MCP support with detailed examples, corroborated by multiple sources and recent industry adoption announcements.